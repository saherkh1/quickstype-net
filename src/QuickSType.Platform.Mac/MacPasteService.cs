using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Paste;
using TextCopy;

namespace QuickSType.Platform.Mac;

public sealed partial class MacPasteService : IPasteService
{
    private const int PasteSettleMs = 120;
    private readonly ILogger _log;

    public MacPasteService(ILogger<MacPasteService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public async Task PasteAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text)) return;

        string? previous = null;
        try
        {
            previous = await ClipboardService.GetTextAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Could not read previous clipboard");
        }

        await ClipboardService.SetTextAsync(text, cancellationToken);

        try
        {
            SendCmdV();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Cmd+V synthesis failed; falling back to AppleScript");
            FallbackPbpaste();
        }

        await Task.Delay(PasteSettleMs, cancellationToken);

        if (previous is not null)
        {
            try { await ClipboardService.SetTextAsync(previous, cancellationToken); }
            catch (Exception ex) { _log.LogDebug(ex, "Could not restore clipboard"); }
        }
    }

    private static void SendCmdV()
    {
        const ushort kVK_ANSI_V = 0x09;
        const ulong kCGEventFlagMaskCommand = 0x00100000UL;

        var src = CGEventSourceCreate(0);
        try
        {
            var keyDown = CGEventCreateKeyboardEvent(src, kVK_ANSI_V, true);
            CGEventSetFlags(keyDown, kCGEventFlagMaskCommand);
            CGEventPost(0, keyDown);
            CFRelease(keyDown);

            var keyUp = CGEventCreateKeyboardEvent(src, kVK_ANSI_V, false);
            CGEventSetFlags(keyUp, kCGEventFlagMaskCommand);
            CGEventPost(0, keyUp);
            CFRelease(keyUp);
        }
        finally
        {
            if (src != IntPtr.Zero) CFRelease(src);
        }
    }

    private static void FallbackPbpaste()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/usr/bin/osascript",
            Arguments = "-e 'tell application \"System Events\" to keystroke \"v\" using {command down}'",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(2000);
    }

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial IntPtr CGEventSourceCreate(int stateID);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.SysInt)]
    private static partial IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial void CGEventSetFlags(IntPtr eventRef, ulong flags);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial void CGEventPost(int tap, IntPtr eventRef);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr cf);
}
