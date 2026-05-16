using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Paste;
using QuickSType.Core.Transcribe;

namespace QuickSType.Platform.Windows;

public sealed partial class WindowsIncrementalInjector : IIncrementalInjector
{
    private const ushort VK_BACK = 0x08;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private readonly ILogger _log;

    public WindowsIncrementalInjector(ILogger<WindowsIncrementalInjector>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public async Task ApplyAsync(TranscriptUpdate update, CancellationToken cancellationToken = default)
    {
        if (update.RetractChars <= 0 && string.IsNullOrEmpty(update.AppendText)) return;
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Send backspace for each retract char
            if (update.RetractChars > 0)
            {
                var backspaceInputs = new INPUT[update.RetractChars * 2];
                for (int i = 0; i < update.RetractChars; i++)
                {
                    backspaceInputs[i * 2] = NewVkKey(VK_BACK, false);
                    backspaceInputs[i * 2 + 1] = NewVkKey(VK_BACK, true);
                }
                uint sent = SendInput((uint)backspaceInputs.Length, backspaceInputs, Marshal.SizeOf<INPUT>());
                if (sent < (uint)backspaceInputs.Length)
                    _log.LogWarning("SendInput backspace: sent {Sent}/{Expected}", sent, backspaceInputs.Length);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Send Unicode text using KEYEVENTF_UNICODE
            if (!string.IsNullOrEmpty(update.AppendText))
            {
                var chars = update.AppendText.ToCharArray();
                var unicodeInputs = new INPUT[chars.Length * 2];
                for (int i = 0; i < chars.Length; i++)
                {
                    unicodeInputs[i * 2] = NewUnicodeKey(chars[i], false);
                    unicodeInputs[i * 2 + 1] = NewUnicodeKey(chars[i], true);
                }
                uint sent = SendInput((uint)unicodeInputs.Length, unicodeInputs, Marshal.SizeOf<INPUT>());
                if (sent < (uint)unicodeInputs.Length)
                    _log.LogWarning("SendInput unicode: sent {Sent}/{Expected}", sent, unicodeInputs.Length);
            }
        }
        catch (OperationCanceledException)
        {
            _log.LogDebug("WindowsIncrementalInjector cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WindowsIncrementalInjector.ApplyAsync failed");
        }

        await Task.CompletedTask;
    }

    private static INPUT NewVkKey(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            }
        }
    };

    private static INPUT NewUnicodeKey(char ch, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0u),
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);
}
