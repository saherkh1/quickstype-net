using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

// RISK: kTISNotifySelectedKeyboardInputSourceChanged is an observed/empirical constant,
// not a documented public API. If removed in a future macOS version, this service degrades
// to polling fallback.
public sealed partial class MacKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private const string HIToolboxLib = "/System/Library/Frameworks/Carbon.framework/Frameworks/HIToolbox.framework/HIToolbox";
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private static readonly IntPtr s_localizedNameKey;
    private static readonly IntPtr s_inputSourceIDKey;
    private static readonly IntPtr s_distributedCenter;

    private readonly ILogger _log;
    private readonly CFNotificationCallback _callback;
    private InputLayout _current;

    static MacKeyboardLayoutService()
    {
        var hitoolbox = NativeLibrary.Load(HIToolboxLib);
        s_localizedNameKey = NativeLibrary.GetExport(hitoolbox, "kTISPropertyLocalizedName");
        s_inputSourceIDKey = NativeLibrary.GetExport(hitoolbox, "kTISPropertyInputSourceID");
        s_distributedCenter = CFNotificationCenterGetDistributedCenter();
    }

    public MacKeyboardLayoutService(ILogger<MacKeyboardLayoutService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _callback = OnLayoutNotification;
        _current = QueryCurrentLayout();

        var name = CFStringCreateWithCString(IntPtr.Zero,
            "kTISNotifySelectedKeyboardInputSourceChanged", 0x08000100u);
        if (name != IntPtr.Zero)
        {
            CFNotificationCenterAddObserver(
                s_distributedCenter,
                IntPtr.Zero,
                _callback,
                name,
                IntPtr.Zero,
                (int)CFNotificationSuspensionBehavior.DeliverImmediately);
            CFRelease(name);
        }
    }

    public InputLayout CurrentLayout => _current;
    public event Action<InputLayout>? LayoutChanged;

    private InputLayout QueryCurrentLayout()
    {
        var src = TISCopyCurrentKeyboardInputSource();
        if (src == IntPtr.Zero)
        {
            _log.LogWarning("TISCopyCurrentKeyboardInputSource returned null");
            return new InputLayout("—", "");
        }

        try
        {
            var displayNamePtr = TISGetInputSourceProperty(src, s_localizedNameKey);
            var displayName = CFStringToString(displayNamePtr) ?? "—";

            var codePtr = TISGetInputSourceProperty(src, s_inputSourceIDKey);
            var rawCode = CFStringToString(codePtr) ?? "";

            return new InputLayout(displayName, ExtractLayoutCode(rawCode));
        }
        finally
        {
            CFRelease(src);
        }
    }

    private void OnLayoutNotification(IntPtr center, IntPtr observer, IntPtr name, IntPtr obj, IntPtr userInfo)
    {
        try
        {
            var newLayout = QueryCurrentLayout();
            _current = newLayout;

            try { LayoutChanged?.Invoke(newLayout); }
            catch (Exception ex) { _log.LogError(ex, "LayoutChanged handler threw"); }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to query layout after notification");
        }
    }

    public void Dispose()
    {
        CFNotificationCenterRemoveEveryObserver(s_distributedCenter, IntPtr.Zero);
    }

    private static string ExtractLayoutCode(string rawCode)
    {
        var parts = rawCode.Split('.');
        return parts.Length > 0 ? parts[^1] : rawCode;
    }

    private static string? CFStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero) return null;

        var ptr = CFStringGetCStringPtr(cfString, 0x08000100u);
        if (ptr != IntPtr.Zero)
            return Marshal.PtrToStringUTF8(ptr);

        var length = CFStringGetLength(cfString);
        if (length <= 0) return null;

        var maxSize = length * 4 + 64;
        var buffer = new byte[maxSize];
        if (CFStringGetCString(cfString, buffer, maxSize, 0x08000100u))
        {
            var nullIndex = Array.IndexOf<byte>(buffer, 0);
            return System.Text.Encoding.UTF8.GetString(buffer, 0, nullIndex > 0 ? nullIndex : buffer.Length);
        }
        return null;
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

    [DllImport(CoreFoundationLib)]
    private static extern IntPtr CFStringGetCStringPtr(IntPtr theString, uint encoding);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, nint bufferSize, uint encoding);

    [LibraryImport(CoreFoundationLib)]
    private static partial nint CFStringGetLength(IntPtr theString);

    [LibraryImport(HIToolboxLib)]
    private static partial IntPtr TISCopyCurrentKeyboardInputSource();

    [LibraryImport(HIToolboxLib)]
    private static partial IntPtr TISGetInputSourceProperty(IntPtr source, IntPtr propertyKey);
}
