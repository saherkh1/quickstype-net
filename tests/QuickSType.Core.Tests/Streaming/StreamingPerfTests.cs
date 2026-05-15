using System.Threading.Channels;
using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class StreamingPerfTests
{
    private const string FixturePath = "tests/fixtures/silence_10s.wav";

    [Fact(Timeout = 11 * 60 * 1000)]
    [Trait("Category", "Slow")]
    public async Task Continuous_10min_produces_no_gen2_gc_and_no_monotonic_ram_growth()
    {
        var modelId = ModelCatalog.Default.Id;
        var modelPath = ModelCatalog.PathFor(modelId);
        if (!File.Exists(modelPath)) return;

        var fixture = LocateFixture();
        if (fixture is null) return;

        var loopSamples = ReadPcm16WavAsFloat(fixture);
        if (loopSamples.Length == 0) return;

        var vad = new VadGate();
        var filter = new HallucinationFilter(HallucinationManifestLoader.Load(modelId));
        using var pipeline = new StreamingPipeline(vad, filter);
        try { pipeline.EnsureLoaded(modelPath, useGpu: true); }
        catch (Exception) { return; } // native Whisper runtime not available in test context

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        var gen2Before = GC.CollectionCount(2);
        var memBaseline = GC.GetTotalMemory(forceFullCollection: false);

        var samples = new List<long>(capacity: 64);

        var channel = Channel.CreateBounded<ReadOnlyMemory<float>>(new BoundedChannelOptions(64)
        {
            SingleReader = true, SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        var producer = Task.Run(async () =>
        {
            const int frameSize = 1024;
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    for (int off = 0; off + frameSize <= loopSamples.Length; off += frameSize)
                    {
                        if (cts.IsCancellationRequested) break;
                        channel.Writer.TryWrite(new ReadOnlyMemory<float>(loopSamples, off, frameSize));
                        await Task.Delay(64, cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally { channel.Writer.TryComplete(); }
        }, cts.Token);

        using var memTimer = new Timer(_ =>
        {
            samples.Add(GC.GetTotalMemory(forceFullCollection: false));
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        try
        {
            await foreach (var _ in pipeline.RunAsync(channel.Reader, sampleRate: 16000, new AppConfig(), cts.Token))
            {
            }
        }
        catch (OperationCanceledException) { }

        try { await producer; }
        catch (OperationCanceledException) { }

        var gen2After = GC.CollectionCount(2);
        var memEnd = GC.GetTotalMemory(forceFullCollection: false);

        (gen2After - gen2Before).ShouldBe(0, $"no gen-2 GCs allowed during 10-min continuous dictation; baseline={gen2Before} end={gen2After}");

        if (samples.Count >= 10)
        {
            samples.Sort();
            var p05 = samples[samples.Count * 5 / 100];
            var p95 = samples[samples.Count * 95 / 100];
            if (p05 > 0)
            {
                ((double)p95 / p05).ShouldBeLessThan(1.20, $"RAM grew monotonically: p05={p05}, p95={p95}, end={memEnd}, baseline={memBaseline}");
            }
        }
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
