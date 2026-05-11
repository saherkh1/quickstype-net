// NOTE: Windows-only implementation. Compiled on macOS but functionally tested on CI Windows runner only.
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Windows;

public sealed partial class WindowsSystemThemeService : ISystemThemeService, IDisposable
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string AdvApi32 = "advapi32.dll";
    private const string DwmApi = "dwmapi.dll";
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
    private const uint HKEY_CURRENT_USER = 0x80000001;
    private const uint KEY_READ = 0x20019;
    private const uint REG_DWORD = 4;

    private readonly ILogger _log;
    private readonly WndProcDelegate _wndProc;
    private readonly IntPtr _hwnd;
    private IntPtr _oldWndProc;
    private AppTheme _current;
    private CancellationTokenSource? _debounceCts;

    public WindowsSystemThemeService(ILogger<WindowsSystemThemeService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;

        _wndProc = WndProc;
        _current = QueryCurrentTheme();

        var hInstance = GetModuleHandleW(null);
        _hwnd = CreateWindowExW(
            0, "STATIC", "QST_Theme",
            0, 0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero, hInstance);

        if (_hwnd != IntPtr.Zero)
        {
            _oldWndProc = SetWindowLongPtrW(_hwnd, -4, Marshal.GetFunctionPointerForDelegate(_wndProc));
        }
    }

    public AppTheme Current => _current;
    public event Action<AppTheme>? Changed;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_SETTINGCHANGE)
        {
            var lParamStr = Marshal.PtrToStringUni(lParam);
            if (string.Equals(lParamStr, "ImmersiveColorSet", StringComparison.OrdinalIgnoreCase))
            {
                DebounceThemeChange();
            }
        }
        else if (msg == WM_DWMCOLORIZATIONCOLORCHANGED)
        {
            DebounceThemeChange();
        }

        return CallWindowProcW(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void DebounceThemeChange()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Delay(100, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
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
                    _log.LogError(ex, "Failed to query theme after change");
                }
            }
        }, token);
    }

    private AppTheme QueryCurrentTheme()
    {
        var isDark = !ReadAppsUseLightTheme();
        var accentHex = ReadAccentColor();
        return new AppTheme(isDark, accentHex);
    }

    private bool ReadAppsUseLightTheme()
    {
        try
        {
            var result = RegOpenKeyExW(
                new IntPtr((long)HKEY_CURRENT_USER),
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                0, KEY_READ, out var hKey);

            if (result != 0 || hKey == IntPtr.Zero) return true;
            try
            {
                var data = new byte[4];
                uint size = 4;
                result = RegQueryValueExW(hKey, "AppsUseLightTheme", IntPtr.Zero, out var type, data, ref size);
                return result == 0 && type == REG_DWORD && BitConverter.ToUInt32(data, 0) == 1;
            }
            finally
            {
                RegCloseKey(hKey);
            }
        }
        catch
        {
            return true;
        }
    }

    private static string ReadAccentColor()
    {
        try
        {
            var hr = DwmGetColorizationColor(out var color, out _);
            if (hr == 0)
            {
                // color is 0xAARRGGBB format from DWM
                return $"#{(color >> 16) & 0xFF:X2}{(color >> 8) & 0xFF:X2}{color & 0xFF:X2}";
            }
        }
        catch
        {
            // DwmGetColorizationColor not available
        }

        return "#007AFF";
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
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

    [LibraryImport(Kernel32, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandleW(string? lpModuleName);

    [LibraryImport(AdvApi32, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegOpenKeyExW(IntPtr hKey, string lpSubKey, uint ulOptions, uint samDesired, out IntPtr phkResult);

    [LibraryImport(AdvApi32, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegQueryValueExW(IntPtr hKey, string lpValueName, IntPtr lpReserved, out uint lpType, byte[] lpData, ref uint lpcbData);

    [LibraryImport(AdvApi32)]
    private static partial int RegCloseKey(IntPtr hKey);

    [LibraryImport(DwmApi)]
    private static partial int DwmGetColorizationColor(out uint pcrColorization, [MarshalAs(UnmanagedType.I1)] out bool pfOpaqueBlend);

}
