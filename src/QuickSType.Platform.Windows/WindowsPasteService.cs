using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Paste;
using TextCopy;

namespace QuickSType.Platform.Windows;

public sealed partial class WindowsPasteService : IPasteService
{
    private const int PasteSettleMs = 120;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private readonly ILogger _log;

    public WindowsPasteService(ILogger<WindowsPasteService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public async Task PasteAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text)) return;

        string? previous = null;
        try { previous = await ClipboardService.GetTextAsync(cancellationToken); }
        catch (Exception ex) { _log.LogDebug(ex, "Could not read previous clipboard"); }

        await ClipboardService.SetTextAsync(text, cancellationToken);

        try
        {
            SendCtrlV();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SendInput Ctrl+V failed");
        }

        await Task.Delay(PasteSettleMs, cancellationToken);

        if (previous is not null)
        {
            try { await ClipboardService.SetTextAsync(previous, cancellationToken); }
            catch (Exception ex) { _log.LogDebug(ex, "Could not restore clipboard"); }
        }
    }

    private static void SendCtrlV()
    {
        var inputs = new INPUT[4];
        inputs[0] = NewKey(VK_CONTROL, false);
        inputs[1] = NewKey(VK_V, false);
        inputs[2] = NewKey(VK_V, true);
        inputs[3] = NewKey(VK_CONTROL, true);
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT NewKey(ushort vk, bool keyUp)
    {
        var input = new INPUT
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
        return input;
    }

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
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);
}
