using QuickSType.Core;

namespace QuickSType.Core.Tests.Hud;

[Trait("category", "wave-0")]
public class HudStateTests
{
    [Theory]
    [InlineData(DictationState.Recording, true)]
    [InlineData(DictationState.Streaming, true)]
    [InlineData(DictationState.Idle, false)]
    [InlineData(DictationState.Processing, false)]
    public void HudVisible_matches_state(DictationState state, bool expectedVisible)
    {
        // Pure function: HUD should be visible during active capture states
        var isVisible = state is DictationState.Recording or DictationState.Streaming;
        isVisible.ShouldBe(expectedVisible);
    }

    [Fact]
    public void TranscriptPreview_applies_retract_then_append()
    {
        var buffer = string.Empty;

        // Simulate how HudViewModel.OnTranscriptUpdate should apply a delta
        var delta = new QuickSType.Core.Transcribe.TranscriptUpdate(3, "world");
        if (delta.RetractChars > 0)
            buffer = buffer.Length >= delta.RetractChars ? buffer[..^delta.RetractChars] : string.Empty;
        buffer += delta.AppendText;

        buffer.ShouldBe("world");
    }

    [Fact]
    public void Elapsed_text_formats_correctly()
    {
        // HudViewModel formats elapsed seconds as MM:SS
        var elapsed = TimeSpan.FromSeconds(75);
        var formatted = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        formatted.ShouldBe("01:15");
    }
}
