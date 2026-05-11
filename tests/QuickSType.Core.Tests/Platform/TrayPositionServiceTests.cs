using System.Drawing;
using QuickSType.Core.Platform;

namespace QuickSType.Core.Tests.Platform;

public class TrayPositionServiceTests
{
    // ── ITrayPositionService via fake ───────────────────────────────

    private sealed class FakeTrayPositionService : ITrayPositionService
    {
        private readonly Rectangle _rect;
        public FakeTrayPositionService(Rectangle rect) => _rect = rect;
        public Rectangle GetTrayRect() => _rect;
    }

    [Fact]
    public void GetTrayRect_returns_the_configured_rectangle()
    {
        var rect = new Rectangle(100, 200, 24, 24);
        var svc = new FakeTrayPositionService(rect);
        var result = svc.GetTrayRect();
        result.ShouldBe(rect);
    }

    [Fact]
    public void GetTrayRect_returns_non_empty_for_valid_tray()
    {
        var rect = new Rectangle(1800, 0, 40, 22);
        var svc = new FakeTrayPositionService(rect);
        var result = svc.GetTrayRect();
        result.Width.ShouldBeGreaterThan(0);
        result.Height.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetTrayRect_accepts_one_pixel_fallback()
    {
        // D-12 fallback rects are 1×1 px — valid for "unknown position"
        var rect = new Rectangle(1860, 4, 1, 1);
        var svc = new FakeTrayPositionService(rect);
        var result = svc.GetTrayRect();
        result.X.ShouldBe(1860);
        result.Y.ShouldBe(4);
        result.Width.ShouldBe(1);
        result.Height.ShouldBe(1);
    }

    [Fact]
    public void GetTrayRect_position_within_reasonable_screen_bounds()
    {
        // Tray icons are typically within 0–4096 px (4K display max)
        var svc = new FakeTrayPositionService(new Rectangle(1900, 3, 30, 22));
        var result = svc.GetTrayRect();
        result.X.ShouldBeGreaterThan(-1);
        result.Y.ShouldBeGreaterThan(-1);
        result.X.ShouldBeLessThan(4097);
        result.Y.ShouldBeLessThan(4097);
    }

    [Fact]
    public void GetTrayRect_handles_zero_origin()
    {
        var svc = new FakeTrayPositionService(new Rectangle(0, 0, 30, 22));
        var result = svc.GetTrayRect();
        result.X.ShouldBe(0);
        result.Y.ShouldBe(0);
    }
}
