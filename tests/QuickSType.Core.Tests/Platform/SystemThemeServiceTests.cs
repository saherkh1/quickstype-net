using QuickSType.Core.Platform;

namespace QuickSType.Core.Tests.Platform;

public class SystemThemeServiceTests
{
    // ── AppTheme record ────────────────────────────────────────────

    [Fact]
    public void AppTheme_constructs_with_IsDark_and_accent_hex()
    {
        var theme = new AppTheme(true, "#000000");
        theme.IsDark.ShouldBeTrue();
        theme.AccentColorHex.ShouldBe("#000000");
    }

    [Fact]
    public void AppTheme_Light_mode_is_false()
    {
        var theme = new AppTheme(false, "#007AFF");
        theme.IsDark.ShouldBeFalse();
        theme.AccentColorHex.ShouldBe("#007AFF");
    }

    [Fact]
    public void AppTheme_value_equality_compares_both_fields()
    {
        var a = new AppTheme(true, "#FF0000");
        var b = new AppTheme(true, "#FF0000");
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void AppTheme_not_equal_when_IsDark_differs()
    {
        var a = new AppTheme(true, "#007AFF");
        var b = new AppTheme(false, "#007AFF");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void AppTheme_not_equal_when_accent_differs()
    {
        var a = new AppTheme(false, "#007AFF");
        var b = new AppTheme(false, "#FF9500");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void AppTheme_handles_six_digit_hex()
    {
        var theme = new AppTheme(true, "#1A2B3C");
        theme.AccentColorHex.ShouldBe("#1A2B3C");
    }

    // ── ISystemThemeService via fake ────────────────────────────────

    private sealed class FakeSystemThemeService : ISystemThemeService
    {
        private AppTheme _current;
        public FakeSystemThemeService(AppTheme initial) => _current = initial;
        public AppTheme Current => _current;
        public event Action<AppTheme>? Changed;

        public void SimulateThemeChange(AppTheme newTheme)
        {
            _current = newTheme;
            Changed?.Invoke(newTheme);
        }
    }

    [Fact]
    public void Current_returns_initial_theme()
    {
        var initial = new AppTheme(false, "#007AFF");
        var svc = new FakeSystemThemeService(initial);
        svc.Current.ShouldBe(initial);
    }

    [Fact]
    public void Changed_fires_when_theme_updates()
    {
        var svc = new FakeSystemThemeService(new AppTheme(false, "#007AFF"));
        AppTheme? received = null;
        svc.Changed += t => received = t;

        var dark = new AppTheme(true, "#1C1C1E");
        svc.SimulateThemeChange(dark);

        received.ShouldNotBeNull();
        received.Value.IsDark.ShouldBeTrue();
        received.Value.ShouldBe(dark);
    }

    [Fact]
    public void Changed_fires_for_accent_color_change()
    {
        var svc = new FakeSystemThemeService(new AppTheme(true, "#007AFF"));
        AppTheme? received = null;
        svc.Changed += t => received = t;

        var newAccent = new AppTheme(true, "#FF9500");
        svc.SimulateThemeChange(newAccent);

        received.ShouldNotBeNull();
        received.Value.AccentColorHex.ShouldBe("#FF9500");
    }

    [Fact]
    public void Changed_handles_multiple_subscribers()
    {
        var svc = new FakeSystemThemeService(new AppTheme(false, "#007AFF"));
        var count = 0;
        svc.Changed += _ => count++;
        svc.Changed += _ => count++;

        svc.SimulateThemeChange(new AppTheme(true, "#000000"));

        count.ShouldBe(2);
    }
}
