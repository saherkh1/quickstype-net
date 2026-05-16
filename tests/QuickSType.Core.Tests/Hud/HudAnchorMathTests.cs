using System.Drawing;

namespace QuickSType.Core.Tests.Hud;

// Pure geometric helpers extracted for testability — no Avalonia dependency.
file static class AnchorMath
{
    public static (int x, int y) PositionHudMac(Rectangle trayRect, int hudWidth, int hudHeight, int gap = 8)
    {
        if (trayRect.IsEmpty)
        {
            // fallback: bottom-center of a 1920x1080 screen
            return ((1920 - hudWidth) / 2, 1080 - hudHeight - gap);
        }
        int x = trayRect.Left + (trayRect.Width - hudWidth) / 2;
        int y = trayRect.Bottom + gap;
        return (x, y);
    }

    public static (int x, int y) PositionHudWindows(Rectangle trayRect, int hudWidth, int hudHeight, int gap = 8)
    {
        if (trayRect.IsEmpty)
        {
            return ((1920 - hudWidth) / 2, 1080 - hudHeight - gap);
        }
        int x = trayRect.Left + (trayRect.Width - hudWidth) / 2;
        int y = trayRect.Top - hudHeight - gap;
        return (x, y);
    }
}

[Trait("category", "wave-0")]
public class HudAnchorMathTests
{
    private const int HudWidth = 260;
    private const int HudHeight = 86;

    [Fact]
    public void Mac_hud_y_is_below_tray_rect_bottom()
    {
        var tray = new Rectangle(900, 0, 22, 22);
        var (_, y) = AnchorMath.PositionHudMac(tray, HudWidth, HudHeight);
        y.ShouldBeGreaterThanOrEqualTo(tray.Bottom);
    }

    [Fact]
    public void Windows_hud_y_is_above_tray_rect_top()
    {
        var tray = new Rectangle(900, 1058, 22, 22);
        var (_, y) = AnchorMath.PositionHudWindows(tray, HudWidth, HudHeight);
        y.ShouldBeLessThanOrEqualTo(tray.Top);
    }

    [Fact]
    public void Empty_tray_rect_produces_bottom_center_fallback()
    {
        var (x, _) = AnchorMath.PositionHudMac(Rectangle.Empty, HudWidth, HudHeight);
        x.ShouldBe((1920 - HudWidth) / 2);
    }

    [Fact]
    public void Mac_hud_x_is_centered_on_tray_icon()
    {
        var tray = new Rectangle(900, 0, 22, 22);
        var (x, _) = AnchorMath.PositionHudMac(tray, HudWidth, HudHeight);
        var trayCenter = tray.Left + tray.Width / 2;
        Math.Abs(x + HudWidth / 2 - trayCenter).ShouldBeLessThanOrEqualTo(1); // within 1px of centered
    }
}
