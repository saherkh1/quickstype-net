// NOTE: Windows-only implementation. Compiled on macOS but functionally tested on CI Windows runner only.
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Windows;

public sealed partial class WindowsTrayPositionService : ITrayPositionService
{
    private const string Shell32 = "shell32.dll";
    private const string User32 = "user32.dll";

    private readonly ILogger _log;
    private IntPtr _hWnd;
    private uint _uId;
    private bool _hasTarget;
    private System.Drawing.Rectangle _lastRect;

    public WindowsTrayPositionService(ILogger<WindowsTrayPositionService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _lastRect = GetFallbackRect();
    }

    /// <summary>Set the tray icon window handle and ID. Called after TrayService.Install().</summary>
    public void SetIconTarget(IntPtr hWnd, uint uId)
    {
        _hWnd = hWnd;
        _uId = uId;
        _hasTarget = true;
    }

    public System.Drawing.Rectangle GetTrayRect()
    {
        if (!_hasTarget)
            return GetFallbackRect();

        var nid = new NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd = _hWnd,
            uID = _uId,
            guidItem = Guid.Empty
        };

        var hr = Shell_NotifyIconGetRect(ref nid, out var rect);
        if (hr == 0)
        {
            _lastRect = new System.Drawing.Rectangle(
                rect.left, rect.top,
                rect.right - rect.left,
                rect.bottom - rect.top);
            return _lastRect;
        }

        _log.LogDebug("Shell_NotifyIconGetRect failed (icon may be in overflow), HR=0x{HR:X8}", hr);
        return GetFallbackRect();
    }

    private System.Drawing.Rectangle GetFallbackRect()
    {
        // D-12 Windows: bottom-center of primary screen working area, 40px above bottom
        var screenW = GetSystemMetrics(78);  // SM_CXVIRTUALSCREEN
        var screenH = GetSystemMetrics(79);  // SM_CYVIRTUALSCREEN
        return new System.Drawing.Rectangle(
            screenW / 2 - 20,
            screenH - 40,
            1, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONIDENTIFIER
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public Guid guidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [LibraryImport(Shell32)]
    private static partial int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    [LibraryImport(User32)]
    private static partial int GetSystemMetrics(int nIndex);
}
