using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

public sealed partial class MacSystemThemeService : ISystemThemeService, IDisposable
{
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string LibObjC = "/usr/lib/libobjc.dylib";

    private readonly ILogger _log;
    private readonly CFNotificationCallback _callback;
    private AppTheme _current;

    public MacSystemThemeService(ILogger<MacSystemThemeService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _callback = OnThemeNotification;
        _current = QueryCurrentTheme();

        var name = CFStringCreateWithCString(IntPtr.Zero,
            "AppleInterfaceThemeChangedNotification", 0x08000100u);
        if (name != IntPtr.Zero)
        {
            CFNotificationCenterAddObserver(
                CFNotificationCenterGetDistributedCenter(),
                IntPtr.Zero,
                _callback,
                name,
                IntPtr.Zero,
                (int)CFNotificationSuspensionBehavior.DeliverImmediately);
            CFRelease(name);
        }
    }

    public AppTheme Current => _current;
    public event Action<AppTheme>? Changed;

    private AppTheme QueryCurrentTheme()
    {
        var isDark = GetAppleInterfaceStyleIsDark();
        var accentHex = GetControlAccentColorHex();
        return new AppTheme(isDark, accentHex);
    }

    private void OnThemeNotification(IntPtr center, IntPtr observer, IntPtr name, IntPtr obj, IntPtr userInfo)
    {
        try
        {
            var newTheme = QueryCurrentTheme();
            _current = newTheme;
            try { Changed?.Invoke(newTheme); }
            catch (Exception ex) { _log.LogError(ex, "Theme change handler threw"); }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to query theme after notification");
        }
    }

    public void Dispose()
    {
        var center = CFNotificationCenterGetDistributedCenter();
        if (center != IntPtr.Zero)
            CFNotificationCenterRemoveEveryObserver(center, IntPtr.Zero);
    }

    private static bool GetAppleInterfaceStyleIsDark()
    {
        // [NSUserDefaults standardUserDefaults] stringForKey:@"AppleInterfaceStyle"]
        var nsUserDefaultsCls = objc_getClass("NSUserDefaults");
        var stdUserDefaultsSel = sel_getUid("standardUserDefaults");
        var defaults = objc_msgSend_IntPtr(nsUserDefaultsCls, stdUserDefaultsSel);
        if (defaults == IntPtr.Zero) return false;

        var stringForKeySel = sel_getUid("stringForKey:");
        var key = CFStringCreateWithCString(IntPtr.Zero, "AppleInterfaceStyle", 0x08000100u);
        if (key == IntPtr.Zero) return false;

        var style = objc_msgSend_IntPtr_IntPtr(defaults, stringForKeySel, key);
        CFRelease(key);
        if (style == IntPtr.Zero) return false; // nil = light mode

        // Compare to "Dark"
        var darkStr = CFStringCreateWithCString(IntPtr.Zero, "Dark", 0x08000100u);
        if (darkStr == IntPtr.Zero) return false;

        var isEqual = CFStringCompare(style, darkStr, 0) == 0;
        CFRelease(darkStr);
        return isEqual;
    }

    private static string GetControlAccentColorHex()
    {
        try
        {
            var nsColorCls = objc_getClass("NSColor");
            var controlAccentSel = sel_getUid("controlAccentColor");
            var color = objc_msgSend_IntPtr(nsColorCls, controlAccentSel);
            if (color == IntPtr.Zero) return "#007AFF";

            // Convert to sRGB colorspace for reliable RGB extraction
            var colorUsingColorSpaceSel = sel_getUid("colorUsingColorSpace:");
            var sRGB = NSColorSpaceSRGB();
            if (sRGB == IntPtr.Zero) return "#007AFF";

            var converted = objc_msgSend_IntPtr_IntPtr(color, colorUsingColorSpaceSel, sRGB);
            if (converted == IntPtr.Zero) converted = color;

            var r = objc_msgSend_Double(converted, sel_getUid("redComponent"));
            var g = objc_msgSend_Double(converted, sel_getUid("greenComponent"));
            var b = objc_msgSend_Double(converted, sel_getUid("blueComponent"));

            return $"#{(byte)(r * 255):X2}{(byte)(g * 255):X2}{(byte)(b * 255):X2}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AccentColor query failed: {ex.Message}");
            return "#007AFF";
        }
    }

    private static IntPtr NSColorSpaceSRGB()
    {
        var nsColorSpaceCls = objc_getClass("NSColorSpace");
        var sRGBSel = sel_getUid("sRGBColorSpace");
        return objc_msgSend_IntPtr(nsColorSpaceCls, sRGBSel);
    }

    private delegate void CFNotificationCallback(IntPtr center, IntPtr observer, IntPtr name, IntPtr obj, IntPtr userInfo);
    private enum CFNotificationSuspensionBehavior { Drop = 1, Coalesce = 2, Hold = 3, DeliverImmediately = 4 }

    [LibraryImport(CoreFoundationLib)]
    private static partial IntPtr CFNotificationCenterGetDistributedCenter();

    [LibraryImport(CoreFoundationLib)]
    private static partial void CFNotificationCenterAddObserver(
        IntPtr center, IntPtr observer, CFNotificationCallback callback,
        IntPtr name, IntPtr obj, int suspensionBehavior);

    [LibraryImport(CoreFoundationLib)]
    private static partial void CFNotificationCenterRemoveEveryObserver(IntPtr center, IntPtr observer);

    [LibraryImport(CoreFoundationLib, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr CFStringCreateWithCString(IntPtr alloc, string value, uint encoding);

    [LibraryImport(CoreFoundationLib)]
    private static partial void CFRelease(IntPtr obj);

    [LibraryImport(CoreFoundationLib)]
    private static partial nint CFStringCompare(IntPtr theString1, IntPtr theString2, nint compareOptions);

    [DllImport(LibObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC, EntryPoint = "sel_getUid")]
    private static extern IntPtr sel_getUid(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern double objc_msgSend_Double(IntPtr receiver, IntPtr selector);
}
