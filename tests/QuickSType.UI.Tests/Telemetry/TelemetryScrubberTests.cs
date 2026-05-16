using QuickSType.UI.Telemetry;

namespace QuickSType.UI.Tests.Telemetry;

public sealed class TelemetryScrubberTests
{
    [Fact]
    public void Scrub_removes_transcribed_text_from_messages()
    {
        var scrubbed = TelemetryScrubber.Scrub("Transcribed: \"email bob@example.com that the launch is delayed\" after decode");

        scrubbed.ShouldContain("Transcribed: [redacted]");
        scrubbed.ShouldNotContain("bob@example.com");
        scrubbed.ShouldNotContain("launch is delayed");
    }

    [Fact]
    public void Scrub_removes_model_paths_and_usernames()
    {
        var scrubbed = TelemetryScrubber.Scrub("Failed loading /Users/saherk/Library/Application Support/QuickSType/models/ggml-small.bin");

        scrubbed.ShouldContain("/Users/[redacted]");
        scrubbed.ShouldNotContain("saherk");
        scrubbed.ShouldNotContain("ggml-small.bin");
    }

    [Fact]
    public void Scrub_removes_windows_model_paths()
    {
        var scrubbed = TelemetryScrubber.Scrub(@"Failed loading C:\Users\Saher\AppData\Roaming\QuickSType\models\ggml-base.bin");

        scrubbed.ShouldContain("[redacted]");
        scrubbed.ShouldNotContain("Saher");
        scrubbed.ShouldNotContain("ggml-base.bin");
    }

    [Fact]
    public void Scrub_removes_audio_device_names()
    {
        var scrubbed = TelemetryScrubber.Scrub("Audio device: 'Saher's AirPods Pro' failed to start");

        scrubbed.ShouldContain("Audio device: [redacted]");
        scrubbed.ShouldNotContain("AirPods");
    }

    [Fact]
    public void Scrub_redacts_sensitive_dictionary_values_by_key()
    {
        var scrubbed = TelemetryScrubber.Scrub(new Dictionary<string, string>
        {
            ["transcript_preview"] = "Call Alice at alice@example.com",
            ["model_path"] = "/Users/saherk/models/ggml-base.bin",
            ["audio-device"] = "MacBook Pro Microphone",
            ["state"] = "recording",
        });

        scrubbed["transcript_preview"].ShouldBe("[redacted]");
        scrubbed["model_path"].ShouldBe("[redacted]");
        scrubbed["audio-device"].ShouldBe("[redacted]");
        scrubbed["state"].ShouldBe("recording");
    }

    [Fact]
    public void Scrub_preserves_non_sensitive_object_values()
    {
        var scrubbed = TelemetryScrubber.Scrub(new Dictionary<string, object?>
        {
            ["duration_ms"] = 412,
            ["backend"] = "cpu",
            ["message"] = "Decode failed for user saher@example.com",
        });

        scrubbed["duration_ms"].ShouldBe(412);
        scrubbed["backend"].ShouldBe("cpu");
        scrubbed["message"].ShouldBe("Decode failed for user [redacted]");
    }
}
