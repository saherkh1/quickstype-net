using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Paste;
using QuickSType.Core.Transcribe;

namespace QuickSType.Platform.Mac;

public sealed partial class MacIncrementalInjector : IIncrementalInjector
{
    private const ushort kVK_Delete = 0x33;
    private readonly ILogger _log;

    public MacIncrementalInjector(ILogger<MacIncrementalInjector>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public async Task ApplyAsync(TranscriptUpdate update, CancellationToken cancellationToken = default)
    {
        if (update.RetractChars <= 0 && string.IsNullOrEmpty(update.AppendText)) return;
        cancellationToken.ThrowIfCancellationRequested();

        var src = CGEventSourceCreate(1); // kCGEventSourceStateCombinedSessionState
        try
        {
            for (int i = 0; i < update.RetractChars; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var keyDown = CGEventCreateKeyboardEvent(src, kVK_Delete, true);
                CGEventPost(0, keyDown);
                CFRelease(keyDown);
                var keyUp = CGEventCreateKeyboardEvent(src, kVK_Delete, false);
                CGEventPost(0, keyUp);
                CFRelease(keyUp);
            }

            if (!string.IsNullOrEmpty(update.AppendText))
            {
                // Send Unicode text by enumerating UTF-16 chars as char pairs/single chars.
                // CGEventKeyboardSetUnicodeString accepts UTF-16 code units.
                var chars = update.AppendText.ToCharArray();
                int i = 0;
                while (i < chars.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Handle surrogate pairs for supplementary characters
                    ushort[] units;
                    if (char.IsHighSurrogate(chars[i]) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
                    {
                        units = [(ushort)chars[i], (ushort)chars[i + 1]];
                        i += 2;
                    }
                    else
                    {
                        units = [(ushort)chars[i]];
                        i++;
                    }

                    var keyDown = CGEventCreateKeyboardEvent(src, 0, true);
                    CGEventKeyboardSetUnicodeString(keyDown, (nuint)units.Length, units);
                    CGEventPost(0, keyDown);
                    CFRelease(keyDown);

                    var keyUp = CGEventCreateKeyboardEvent(src, 0, false);
                    CGEventKeyboardSetUnicodeString(keyUp, (nuint)units.Length, units);
                    CGEventPost(0, keyUp);
                    CFRelease(keyUp);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log.LogDebug("MacIncrementalInjector cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "MacIncrementalInjector.ApplyAsync failed");
        }
        finally
        {
            if (src != IntPtr.Zero) CFRelease(src);
        }

        await Task.CompletedTask;
    }

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial IntPtr CGEventSourceCreate(int stateID);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial void CGEventKeyboardSetUnicodeString(IntPtr eventRef, nuint stringLength, ushort[] unicodeString);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial void CGEventPost(int tap, IntPtr eventRef);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr cf);
}
