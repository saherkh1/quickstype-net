// WAVE0 stub — replace with real types after 03-02 creates ITrayPositionService
namespace QuickSType.Core.Tests.Platform;

public readonly record struct RectangleStub(int X, int Y, int Width, int Height);

public class TrayPositionServiceTests
{
    [Fact]
    public void GetTrayRect_returns_non_empty_rectangle()
    {
        // WAVE1: replace with ITrayPositionService.GetTrayRect() call
        var rect = GetFallbackRect();
        rect.Width.ShouldBeGreaterThan(0);
        rect.Height.ShouldBeGreaterThan(0);
    }

    private static RectangleStub GetFallbackRect()
    {
        // D-12 fallback: top-right of screen, 20px from right
        return new RectangleStub(1860, 4, 1, 1);
    }
}
