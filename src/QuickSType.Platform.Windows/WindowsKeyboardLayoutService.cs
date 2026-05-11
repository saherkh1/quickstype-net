// NOTE: Windows-only implementation. Compiled on macOS but functionally tested on CI Windows runner only.
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Windows;

// WM_INPUTLANGCHANGE may not be reliably delivered to message-only windows for
// background tray apps. If testing shows unreliability, add SetWinEventHook
// fallback for EVENT_OBJECT_INPUTLANGCHANGE.
public sealed partial class WindowsKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const uint LOCALE_SNAME = 0x5c;
    private const uint LOCALE_SLOCALIZEDDISPLAYNAME = 0x02;
    private const uint WM_INPUTLANGCHANGE = 0x0051;

    private readonly ILogger _log;
    private readonly WndProcDelegate _wndProc;
    private readonly IntPtr _hwnd;
    private IntPtr _oldWndProc;
    private InputLayout _current;

    public WindowsKeyboardLayoutService(ILogger<WindowsKeyboardLayoutService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;

        _wndProc = WndProc;
        _current = QueryCurrentLayout();

        // Create message-only window for WM_INPUTLANGCHANGE
        var hInstance = GetModuleHandleW(null);
        _hwnd = CreateWindowExW(
            0, "STATIC", "QST_KBLayout",
            0, 0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero, hInstance);

        if (_hwnd != IntPtr.Zero)
        {
            _oldWndProc = SetWindowLongPtrW(_hwnd, -4, Marshal.GetFunctionPointerForDelegate(_wndProc));
        }
    }

    public InputLayout CurrentLayout => _current;
    public event Action<InputLayout>? LayoutChanged;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_INPUTLANGCHANGE)
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
                _log.LogError(ex, "Failed to query layout after WM_INPUTLANGCHANGE");
            }
        }

        return CallWindowProcW(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private static InputLayout QueryCurrentLayout()
    {
        var hkl = GetKeyboardLayout(0);
        var langId = (uint)((nuint)hkl & 0xFFFF);

        // BCP-47 locale name
        var nameBuffer = new char[256];
        var nameLen = GetLocaleInfoW(langId, LOCALE_SNAME, nameBuffer, nameBuffer.Length);
        var localeName = nameLen > 0 ? new string(nameBuffer, 0, nameLen - 1) : "";

        // Localized display name
        var displayBuffer = new char[256];
        var displayLen = GetLocaleInfoW(langId, LOCALE_SLOCALIZEDDISPLAYNAME, displayBuffer, displayBuffer.Length);
        var displayName = displayLen > 0 ? new string(displayBuffer, 0, displayLen - 1) : "—";

        return new InputLayout(displayName, localeName);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
            DestroyWindow(_hwnd);
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport(User32, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool DestroyWindow(IntPtr hWnd);

    [LibraryImport(User32)]
    private static partial IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport(User32)]
    private static partial IntPtr CallWindowProcW(IntPtr prevWndProc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport(User32)]
    private static partial IntPtr GetKeyboardLayout(uint idThread);

    [LibraryImport(Kernel32, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandleW(string? lpModuleName);

    [LibraryImport(Kernel32, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetLocaleInfoW(uint locale, uint lcType, char[] lpLCData, int cchData);
}
