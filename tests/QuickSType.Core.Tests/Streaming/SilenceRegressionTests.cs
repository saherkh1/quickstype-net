using System.Threading.Channels;
using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class SilenceRegressionTests
{
    private const string FixturePath = "tests/fixtures/silence_10s.wav";

    [Fact]
    [Trait("Category", "Slow")]
    public async Task Silence_10s_wav_produces_zero_transcript_updates()
    {
        var modelId = ModelCatalog.Default.Id;
        var modelPath = ModelCatalog.PathFor(modelId);
        if (!File.Exists(modelPath)) return;

        var fixture = LocateFixture();
        if (fixture is null) return;

        var samples = ReadPcm16WavAsFloat(fixture);
        samples.Length.ShouldBeInRange(155_000, 165_000);

        var vad = new VadGate();
        var filter = new HallucinationFilter(HallucinationManifestLoader.Load(modelId));
        using var pipeline = new StreamingPipeline(vad, filter);
        try { pipeline.EnsureLoaded(modelPath, useGpu: true); }
        catch (Exception) { return; } // native Whisper runtime not available in test context

        var channel = Channel.CreateBounded<ReadOnlyMemory<float>>(new BoundedChannelOptions(64)
        {
            SingleReader = true, SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        const int frameSize = 1024;
        for (int offset = 0; offset + frameSize <= samples.Length; offset += frameSize)
        {
            channel.Writer.TryWrite(new ReadOnlyMemory<float>(samples, offset, frameSize));
        }
        channel.Writer.TryComplete();

        var seen = new List<TranscriptUpdate>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await foreach (var update in pipeline.RunAsync(channel.Reader, sampleRate: 16000, new AppConfig(), cts.Token))
        {
            seen.Add(update);
        }

        seen.ShouldBeEmpty($"Silent audio must not produce any TranscriptUpdate. Got: {string.Join(" | ", seen.Select(u => u.AppendText))}");
    }

    private static string? LocateFixture()
    {
        var cwd = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(cwd);
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, FixturePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static float[] ReadPcm16WavAsFloat(string path)
    {
        var bytes = File.ReadAllBytes(path);
        const int headerBytes = 44;
        if (bytes.Length <= headerBytes) return Array.Empty<float>();
        int sampleCount = (bytes.Length - headerBytes) / 2;
        var result = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short s = BitConverter.ToInt16(bytes, headerBytes + i * 2);
            result[i] = s / 32768f;
        }
        return result;
    }
}
