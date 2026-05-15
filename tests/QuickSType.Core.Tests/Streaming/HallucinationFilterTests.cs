using System.Collections.Generic;
using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class HallucinationFilterTests
{
    [Theory]
    [InlineData(0.1f, "hello world", false)]
    [InlineData(0.6f, "", true)]
    [InlineData(0.51f, "Hello world", true)]
    [InlineData(0.5f, "bar", false)]
    public void NoSpeechProbability_threshold_drops_chunk(float avgNoSpeech, string text, bool expected)
    {
        var filter = new HallucinationFilter(new HallucinationManifest());
        filter.ShouldDrop(text, avgNoSpeech).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Thanks for watching", "Thanks for watching!", true)]
    [InlineData("thanks for watching", "Thanks For Watching", true)]
    [InlineData("Thanks for watching", "I was thanking him", false)]
    [InlineData("Subtitles by", "Subtitles by Bob", true)]
    public void Denylist_substring_match_drops_chunk(string denyPhrase, string text, bool expected)
    {
        var manifest = new HallucinationManifest { Phrases = new List<string> { denyPhrase } };
        var filter = new HallucinationFilter(manifest);
        filter.ShouldDrop(text, 0.1f).ShouldBe(expected);
    }

    [Fact]
    public void NoSpeech_threshold_wins_even_when_denylist_misses()
    {
        var manifest = new HallucinationManifest { Phrases = new List<string> { "Subtitles by" } };
        var filter = new HallucinationFilter(manifest);
        filter.ShouldDrop("Hello world", 0.51f).ShouldBeTrue();
    }
}
