using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class VadGateBindingTests
{
    [Fact]
    public void Feed_with_continuous_speech_tone_emits_at_least_one_chunk()
    {
        // 440Hz sine wave triggers both Silero VAD (if model present) and RMS energy fallback.
        // Force-flush at MaxChunkMs=1s guarantees a chunk even without speech->silence transition.
        // This test fails ONLY if RunVadOn512Samples returns false unconditionally (the stub behavior)
        // because then _hasSpeechStarted never becomes true and force-flush never triggers.
        using var gate = new VadGate(sampleRate: 16000);

        int totalFrames = (int)Math.Ceiling(1.5 * 16000.0 / VadGate.SileroWindowSamples);
        bool sawChunk = false;
        for (int i = 0; i < totalFrames; i++)
        {
            var speech = new float[VadGate.SileroWindowSamples];
            for (int s = 0; s < speech.Length; s++)
            {
                double t = (i * VadGate.SileroWindowSamples + s) / 16000.0;
                speech[s] = (float)(0.6 * Math.Sin(2 * Math.PI * 440 * t));
            }
            if (gate.Feed(speech) is not null) sawChunk = true;
        }
        if (gate.FinalFlush() is not null) sawChunk = true;

        sawChunk.ShouldBeTrue(
            "VadGate must emit at least one chunk for continuous sine wave input. " +
            "Failure indicates RunVadOn512Samples returns false for all input (stub behavior).");
    }

    [Fact]
    public void Feed_with_pure_silence_emits_no_chunks()
    {
        using var gate = new VadGate(sampleRate: 16000);

        for (int i = 0; i < 64; i++) // 64 * 512 = 32768 samples ≈ 2s
        {
            gate.Feed(new float[VadGate.SileroWindowSamples]).ShouldBeNull();
        }
        gate.FinalFlush().ShouldBeNull();
    }
}
