// WAVE0 stub — verifies the detected-layout header format (PLAT-02)
namespace QuickSType.Core.Tests.Platform;

public class TrayMenuLayoutHeaderTests
{
    [Fact]
    public void Detected_header_format_with_display_name()
    {
        // WAVE1: replace with IKeyboardLayoutService integration test
        var displayName = "English (US)";
        var header = FormatDetectedHeader(displayName);
        header.ShouldBe("Detected: English (US)");
    }

    [Fact]
    public void Detected_header_em_dash_fallback_when_null()
    {
        var header = FormatDetectedHeader(null);
        header.ShouldBe("Detected: —");
    }

    [Fact]
    public void Detected_header_em_dash_fallback_when_empty()
    {
        var header = FormatDetectedHeader("");
        header.ShouldBe("Detected: —");
    }

    private static string FormatDetectedHeader(string? displayName) =>
        !string.IsNullOrEmpty(displayName) ? $"Detected: {displayName}" : "Detected: —";
}
