using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

// macOS 26 (Sequoia 2 / 2026) removed the HIToolbox dylib binary.
// TISCopyCurrentKeyboardInputSource and friends no longer exist as loadable symbols.
// We use `defaults read` against the HIToolbox plist instead.
public sealed partial class MacKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // Literal string value of kTISNotifySelectedKeyboardInputSourceChanged.
    // COULD-BREAK: undocumented constant; verified against macOS 14 headers.
    private const string TISNotificationName = "com.apple.Carbon.TISNotifySelectedKeyboardInputSourceChanged";

    private readonly ILogger _log;
    private readonly CFNotificationCallback _callback;
    private readonly Timer _pollTimer;
    private InputLayout _current;

    public MacKeyboardLayoutService(ILogger<MacKeyboardLayoutService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _callback = OnLayoutNotification;
        _current = QueryCurrentLayout();

        // CFNotificationCenter for real-time notification when it works.
        // Falls back to 1s polling if the notification name changes in a future macOS.
        var name = CFStringCreateWithCString(IntPtr.Zero, TISNotificationName, 0x08000100u);
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

        // Polling safety net (500ms). Cheap — layout changes are rare.
        _pollTimer = new Timer(_ => OnLayoutNotification(
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero),
            null, 500, 500);
    }

    public InputLayout CurrentLayout => _current;
    public event Action<InputLayout>? LayoutChanged;

    private InputLayout QueryCurrentLayout()
    {
        try
        {
            var (displayName, layoutId) = ReadLayoutFromDefaults();
            var code = MapLayoutToCode(layoutId, displayName);
            return new InputLayout(displayName, code);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to read keyboard layout from defaults");
            return new InputLayout("--", "");
        }
    }

    private static (string name, int id) ReadLayoutFromDefaults()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/defaults",
                Arguments = "read ~/Library/Preferences/com.apple.HIToolbox.plist AppleSelectedInputSources",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(2000);

        // Find the first "Keyboard Layout" entry:
        //   InputSourceKind = "Keyboard Layout";
        //   "KeyboardLayout Name" = ABC;
        //   "KeyboardLayout ID" = 252;
        var lines = output.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Keyboard Layout"))
            {
                string name = "";
                int id = 0;

                for (int j = i; j < Math.Min(i + 5, lines.Length); j++)
                {
                    var line = lines[j];
                    if (line.Contains("KeyboardLayout Name"))
                        name = ExtractPlistValue(line);
                    else if (line.Contains("KeyboardLayout ID"))
                        int.TryParse(ExtractPlistValue(line), out id);
                }

                if (!string.IsNullOrEmpty(name))
                    return (name, id);
            }
        }

        return ("--", 0);
    }

    private static string ExtractPlistValue(string line)
    {
        var eq = line.IndexOf('=');
        if (eq < 0) return "";
        return line[(eq + 1)..].Trim().TrimEnd(';').Trim('"');
    }

    private static string MapLayoutToCode(int layoutId, string displayName)
    {
        var code = layoutId switch
        {
            0 => "en-US",       // ABC (default, reported as 0 on some versions)
            252 => "en-US",     // ABC
            -2 => "en-US",      // U.S.
            -10000 => "ar",     // Arabic
            -18944 => "he",     // Hebrew
            -23552 => "ru",     // Russian
            -26624 => "zh-Hans", // Chinese Simplified
            -27008 => "zh-Hant", // Chinese Traditional
            -14848 => "ja",     // Japanese
            -15360 => "ko",     // Korean
            _ => "",
        };

        if (!string.IsNullOrEmpty(code)) return code;

        return displayName switch
        {
            "ABC" => "en-US",
            "Arabic" => "ar",
            "Hebrew" => "he",
            "Russian" => "ru",
            _ => "",
        };
    }

    private void OnLayoutNotification(IntPtr center, IntPtr observer, IntPtr name, IntPtr obj, IntPtr userInfo)
    {
        try
        {
            var newLayout = QueryCurrentLayout();
            if (!newLayout.Equals(_current))
            {
                _current = newLayout;
                try { LayoutChanged?.Invoke(newLayout); }
                catch (Exception ex) { _log.LogError(ex, "LayoutChanged handler threw"); }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to query layout after notification");
        }
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
        var center = CFNotificationCenterGetDistributedCenter();
        if (center != IntPtr.Zero)
            CFNotificationCenterRemoveEveryObserver(center, IntPtr.Zero);
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
}
