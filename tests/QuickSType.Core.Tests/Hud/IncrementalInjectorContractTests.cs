using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests.Hud;

[Trait("category", "wave-0")]
public class IncrementalInjectorContractTests
{
    [Fact]
    public void AppendOnly_delta_has_zero_retract()
    {
        var update = new TranscriptUpdate(0, "hello");
        update.RetractChars.ShouldBe(0);
        update.AppendText.ShouldBe("hello");
    }

    [Fact]
    public void RetractOnly_delta_has_empty_append()
    {
        var update = new TranscriptUpdate(3, string.Empty);
        update.RetractChars.ShouldBe(3);
        update.AppendText.ShouldBeEmpty();
    }

    [Fact]
    public void RetractAndAppend_delta_combines_both()
    {
        var update = new TranscriptUpdate(2, "world");
        update.RetractChars.ShouldBe(2);
        update.AppendText.ShouldBe("world");
    }

    [Fact]
    public void Sequential_deltas_build_correct_text()
    {
        // Simulate applying sequential deltas to a text buffer
        var buffer = string.Empty;
        var deltas = new[]
        {
            new TranscriptUpdate(0, "helo"),
            new TranscriptUpdate(2, "llo world"),
        };
        foreach (var d in deltas)
        {
            if (d.RetractChars > 0)
                buffer = buffer.Length >= d.RetractChars
                    ? buffer[..^d.RetractChars]
                    : string.Empty;
            buffer += d.AppendText;
        }
        buffer.ShouldBe("hello world");
    }
}
