// WAVE0 stub — replace AppThemeStub with AppTheme after 03-02
namespace QuickSType.Core.Tests.Platform;

public readonly record struct AppThemeStub(bool IsDark, string AccentColorHex);

public class SystemThemeServiceTests
{
    [Fact]
    public void AppTheme_stores_IsDark()
    {
        var dark = new AppThemeStub(true, "#000000");
        dark.IsDark.ShouldBeTrue();
    }

    [Fact]
    public void AppTheme_stores_AccentColorHex()
    {
        var theme = new AppThemeStub(false, "#007AFF");
        theme.AccentColorHex.ShouldBe("#007AFF");
    }

    [Fact]
    public void AppTheme_is_readonly_record()
    {
        var a = new AppThemeStub(false, "#007AFF");
        var b = new AppThemeStub(false, "#007AFF");
        a.ShouldBe(b);
    }
}
