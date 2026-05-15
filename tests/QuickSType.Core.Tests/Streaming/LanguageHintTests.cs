using QuickSType.Core;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class LanguageHintTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("en", "en")]
    [InlineData("zh-Hans", "zh")]
    [InlineData("pt-BR", "pt")]
    [InlineData("EN-us", "en")]
    [InlineData("xx-YY", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("  en-US  ", "en")]
    public void LayoutCodeToWhisperLang_maps_bcp47_to_iso639(string? layoutCode, string? expected)
    {
        Languages.LayoutCodeToWhisperLang(layoutCode).ShouldBe(expected);
    }
}
