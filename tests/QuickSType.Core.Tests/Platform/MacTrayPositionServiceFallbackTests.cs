namespace QuickSType.Core.Tests.Platform;

#if MACOS_PLATFORM
// Regression test for debug session hud-not-showing (2026-05-24):
// MacTrayPositionService.ComputeFallbackRect previously computed Y as
// `bounds.Y + bounds.Height + 4` — placing the rect BELOW the display
// (off-screen). The fallback rect must be near the TOP of the display
// (where the macOS menu bar / status items live), not the bottom.
public class MacTrayPositionServiceFallbackTests
{
    [Fact]
    public void ComputeFallbackRect_places_y_near_top_of_display_not_bottom()
    {
        if (!OperatingSystem.IsMacOS()) return;

        // 1440x900 primary display, origin (0, 0).
        var rect = QuickSType.Platform.Mac.MacTrayPositionService.ComputeFallbackRect(0, 0, 1440, 900);

        // Y must be near the top (small positive value), NOT below the screen.
        rect.Y.ShouldBeGreaterThanOrEqualTo(0);
        rect.Y.ShouldBeLessThan(100); // menu bar area
        rect.Y.ShouldBeLessThan(900); // strictly above bottom edge

        // X is near the right edge.
        rect.X.ShouldBeGreaterThan(1400);
        rect.X.ShouldBeLessThan(1440);

        rect.Width.ShouldBe(1);
        rect.Height.ShouldBe(1);
    }

    [Fact]
    public void ComputeFallbackRect_respects_nonzero_origin_displays()
    {
        if (!OperatingSystem.IsMacOS()) return;

        // Secondary display at (1440, 0), 1920x1080.
        var rect = QuickSType.Platform.Mac.MacTrayPositionService.ComputeFallbackRect(1440, 0, 1920, 1080);

        // Y still near top of the display (its own coordinate space).
        rect.Y.ShouldBeLessThan(100);
        // X near right edge of THIS display, not below it.
        rect.X.ShouldBeGreaterThan(1440);
        rect.X.ShouldBeLessThanOrEqualTo(1440 + 1920);
    }

    [Fact]
    public void ComputeFallbackRect_4k_display_y_stays_in_menu_bar()
    {
        if (!OperatingSystem.IsMacOS()) return;

        // 4K display: pre-fix bug would put Y at ~2164; fixed code puts Y at 4.
        var rect = QuickSType.Platform.Mac.MacTrayPositionService.ComputeFallbackRect(0, 0, 3840, 2160);
        rect.Y.ShouldBeLessThan(50);
        rect.X.ShouldBeLessThan(3840);
    }
}
#endif
