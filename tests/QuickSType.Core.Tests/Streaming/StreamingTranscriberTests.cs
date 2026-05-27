using System.Runtime.CompilerServices;
using System.Threading.Channels;
using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class StreamingTranscriberTests
{
    [Fact]
    public async Task RunAsync_yields_updates_in_order_until_channel_completes()
    {
        var fake = new FakeStreamer(new[]
        {
            new TranscriptUpdate(0, "Hello "),
            new TranscriptUpdate(0, "world"),
        });

        var channel = Channel.CreateBounded<ReadOnlyMemory<float>>(new BoundedChannelOptions(4)
        {
            SingleReader = true, SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        channel.Writer.TryWrite(new ReadOnlyMemory<float>(new float[16000])).ShouldBeTrue();
        channel.Writer.TryComplete();

        var seen = new List<TranscriptUpdate>();
        await foreach (var update in fake.RunAsync(channel.Reader, sampleRate: 16000, new AppConfig(), CancellationToken.None))
        {
            seen.Add(update);
        }

        seen.Count.ShouldBe(2);
        seen[0].AppendText.ShouldBe("Hello ");
        seen[1].AppendText.ShouldBe("world");
    }

    [Fact]
    public async Task RunAsync_honors_cancellation_token()
    {
        var updates100 = Enumerable.Range(0, 100).Select(i => new TranscriptUpdate(0, $"u{i} ")).ToArray();
        var fake = new FakeStreamer(updates100, delayMs: 50);

        var channel = Channel.CreateBounded<ReadOnlyMemory<float>>(new BoundedChannelOptions(4)
        {
            SingleReader = true, SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        channel.Writer.TryWrite(new ReadOnlyMemory<float>(new float[16000])).ShouldBeTrue();
        // Do NOT complete the writer — the cancellation token must stop the loop.

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var seen = new List<TranscriptUpdate>();
        try
        {
            await foreach (var update in fake.RunAsync(channel.Reader, sampleRate: 16000, new AppConfig(), cts.Token))
            {
                seen.Add(update);
            }
        }
        catch (OperationCanceledException) { /* expected */ }

        seen.Count.ShouldBeLessThan(100);
    }

    private sealed class FakeStreamer : IStreamingTranscriber
    {
        private readonly IReadOnlyList<TranscriptUpdate> _updates;
        private readonly int _delayMs;
        public event Action? DegradeRequested;
        public FakeStreamer(IReadOnlyList<TranscriptUpdate> updates, int delayMs = 0)
        {
            _updates = updates; _delayMs = delayMs;
        }
        public void EnsureLoaded(string modelPath, bool useGpu = true, CancellationToken cancellationToken = default) { }
        public async IAsyncEnumerable<TranscriptUpdate> RunAsync(
            ChannelReader<ReadOnlyMemory<float>> frames, int sampleRate, AppConfig config,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Consume at least one frame so the channel handshake is exercised.
            await foreach (var _ in frames.ReadAllAsync(ct))
            {
                break;
            }
            foreach (var u in _updates)
            {
                ct.ThrowIfCancellationRequested();
                if (_delayMs > 0) await Task.Delay(_delayMs, ct);
                yield return u;
            }
            _ = DegradeRequested;
        }
        public void Dispose() { }
    }
}
