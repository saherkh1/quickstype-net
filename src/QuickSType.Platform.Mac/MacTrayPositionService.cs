using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

// Fallback only implementation. NSStatusItem rect query via objc_msgSend_Stret
// is blocked by an arm64 calling-convention mismatch: .NET P/Invoke passes the
// out-buffer in x0 instead of x8, shifting receiver/selector by one register.
// TODO: re-enable NSStatusBarWindow enumeration once we have a native trampoline
// or use the Accessibility API (AXUIElementCopyAttributeValue).
public sealed class MacTrayPositionService : ITrayPositionService
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    private readonly ILogger _log;
    private System.Drawing.Rectangle _lastRect;

    public MacTrayPositionService(ILogger<MacTrayPositionService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _lastRect = GetFallbackRect();
    }

    public System.Drawing.Rectangle GetTrayRect()
    {
        return GetFallbackRect();
    }

    private static System.Drawing.Rectangle GetFallbackRect()
    {
        try
        {
            var displayId = CGMainDisplayID();
            var bounds = CGDisplayBounds(displayId);
            return ComputeFallbackRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
        catch
        {
            // Hail-Mary hardcoded fallback for 14" MacBook Pro notch-screen.
            return new System.Drawing.Rectangle(1860, 4, 1, 1);
        }
    }

    /// <summary>
    /// Pure math helper: given primary-display bounds in screen coordinates, return the
    /// fallback "where the status item probably is" 1×1 rect.
    ///
    /// D-12 Mac: top-right of primary screen, 20px from right edge, 4px below menu bar.
    /// macOS status items live in the menu bar at the TOP of the display, so Y must be near
    /// bounds.Y (top), NOT bounds.Y + bounds.Height (which is below the bottom edge and
    /// pushes the HUD off-screen — see debug session hud-not-showing).
    /// </summary>
    internal static System.Drawing.Rectangle ComputeFallbackRect(double x, double y, double width, double height)
    {
        return new System.Drawing.Rectangle(
            (int)(x + width - 20),
            (int)(y + 4),
            1, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    [DllImport(CoreGraphicsLib)]
    private static extern CGRect CGDisplayBounds(uint displayID);

    [DllImport(CoreGraphicsLib)]
    private static extern uint CGMainDisplayID();
}
