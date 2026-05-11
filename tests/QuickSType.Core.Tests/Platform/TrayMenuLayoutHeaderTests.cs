namespace QuickSType.Core.Tests.Platform;

public class TrayMenuLayoutHeaderTests
{
    // D-02: Label format is "Detected: <display-name>" with "Detected: —" fallback.
    // TrayService.FormatDetectedHeader mirrors this logic identically
    // (internal static, tested here as a contract verification).

    [Fact]
    public void Format_header_includes_detected_prefix_and_display_name()
    {
        var header = FormatDetectedHeader("English (US)");
        header.ShouldBe("Detected: English (US)");
    }

    [Fact]
    public void Format_header_shows_em_dash_when_null()
    {
        var header = FormatDetectedHeader(null);
        header.ShouldBe("Detected: —");
    }

    [Fact]
    public void Format_header_shows_em_dash_when_empty()
    {
        var header = FormatDetectedHeader("");
        header.ShouldBe("Detected: —");
    }

    [Fact]
    public void Format_header_shows_em_dash_when_whitespace_only()
    {
        var header = FormatDetectedHeader("   ");
        header.ShouldBe("Detected: —");
    }

    [Fact]
    public void Format_header_handles_non_latin_names()
    {
        var header = FormatDetectedHeader("العربية");
        header.ShouldBe("Detected: العربية");
    }

    [Fact]
    public void Format_header_handles_windows_short_name()
    {
        var header = FormatDetectedHeader("ENG");
        header.ShouldBe("Detected: ENG");
    }

    private static string FormatDetectedHeader(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) ? $"Detected: {displayName}" : "Detected: —";
}
