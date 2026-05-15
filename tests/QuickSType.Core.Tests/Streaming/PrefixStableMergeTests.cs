using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class PrefixStableMergeTests
{
    [Theory]
    [InlineData("", "hello world", 0, "hello world")]
    [InlineData("hello world", "hello world", 0, "")]
    [InlineData("hello ", "hello world", 0, "world")]
    [InlineData("hello wor", "hello world", 3, "world")]
    [InlineData("hello world", "hello", 6, "")]
    [InlineData("foo bar", "baz qux", 7, "baz qux")]
    [InlineData("hello world", "hello there", 5, "there")]
    [InlineData("café ", "café latte", 0, "latte")]
    public void Merge_produces_expected_update(string prior, string newHyp, int expectedRetract, string expectedAppend)
    {
        var m = new LocalAgreementMerger();
        if (prior.Length > 0)
        {
            var seed = m.Merge(prior);
            seed.RetractChars.ShouldBe(0);
            seed.AppendText.ShouldBe(prior);
        }
        var update = m.Merge(newHyp);
        update.RetractChars.ShouldBe(expectedRetract);
        update.AppendText.ShouldBe(expectedAppend);
    }

    [Fact]
    public void Reset_clears_internal_state()
    {
        var m = new LocalAgreementMerger();
        m.Merge("foo");
        m.Reset();
        var update = m.Merge("bar");
        update.RetractChars.ShouldBe(0);
        update.AppendText.ShouldBe("bar");
    }
}
