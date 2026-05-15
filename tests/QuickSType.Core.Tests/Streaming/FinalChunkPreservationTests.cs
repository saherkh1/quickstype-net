using System.Threading.Channels;
using QuickSType.Core;
using QuickSType.Core.Audio;
using QuickSType.Core.Config;
using QuickSType.Core.Platform;
using QuickSType.Core.Transcribe;
using Xunit;
using Shouldly;

namespace QuickSType.Core.Tests.Streaming;

public class FinalChunkPreservationTests
{
    [Fact]
    public async Task FinalChunkPreservedAfterHotkeyRelease()
    {
        var streamer = new FinalYieldingStreamer();
        var audio = new ChannelDrivenFakeAudio();
        var engine = NewEngine(audio, streamer);

        var updates = new List<TranscriptUpdate>();
        engine.TranscriptUpdate += u => updates.Add(u);
        var idle = new TaskCompletionSource<bool>();
        engine.StateChanged += s => { if (s == DictationState.Idle) idle.TrySetResult(true); };

        engine.OnHotkeyPressed();
        engine.EffectiveMode.ShouldBe("streaming");

        const int sampleRate = 16000;
        var batch = new float[(int)(0.7 * sampleRate)];
        for (int i = 0; i < batch.Length; i++)
            batch[i] = (float)(0.6 * Math.Sin(2 * Math.PI * 440 * i / sampleRate));
        audio.PushFrames(new ReadOnlyMemory<float>(batch));

        engine.OnHotkeyReleased();

        var winner = await Task.WhenAny(idle.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        winner.ShouldBe(idle.Task, "Engine did not reach Idle within 8s");

        updates.ShouldNotBeEmpty("FinalChunk must be delivered — B3 regression if empty");
    }

    private sealed class FinalYieldingStreamer : IStreamingTranscriber
    {
        public event Action? DegradeRequested;
        public async IAsyncEnumerable<TranscriptUpdate> RunAsync(
            ChannelReader<ReadOnlyMemory<float>> frames, int sampleRate, AppConfig config,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var _ in frames.ReadAllAsync(ct).ConfigureAwait(false)) { }
            _ = DegradeRequested;
            yield return new TranscriptUpdate(0, "final-chunk");
        }
        public void Dispose() { }
    }

    private sealed class ChannelDrivenFakeAudio : IAudioCapture
    {
        private Channel<ReadOnlyMemory<float>>? _channel;
        public int SampleRate => 16000;
        public int Channels => 1;
        public bool IsRecording { get; private set; }
        public ChannelReader<ReadOnlyMemory<float>>? Frames => _channel?.Reader;
        public void Start()
        {
            _channel = Channel.CreateBounded<ReadOnlyMemory<float>>(new BoundedChannelOptions(64)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true,
            });
            IsRecording = true;
        }
        public void PushFrames(ReadOnlyMemory<float> samples) => _channel?.Writer.TryWrite(samples);
        public float[] Stop()
        {
            _channel?.Writer.TryComplete();
            IsRecording = false;
            return Array.Empty<float>();
        }
        public IReadOnlyList<AudioDeviceInfo> ListInputDevices() => Array.Empty<AudioDeviceInfo>();
        public void SelectInputDevice(string? deviceName) { }
        public void Dispose() { }
    }

    private sealed class CapableSpecs : ISystemSpecsService
    {
        public SystemSpecs Detect() => new(16.0, 100.0, false, HardwareTier.AppleSilicon);
        public ModelInfo RecommendModel(SystemSpecs s) => ModelCatalog.Default;
        public string? GetHardwareWarning(ModelInfo m, SystemSpecs s) => null;
        public bool IsBlocker(SystemSpecs s) => false;
        public bool IsStreamingCapable() => true;
        public double MinRamGb => 2.0;
        public double MinFreeDiskGb => 1.0;
    }

    private static DictationEngine NewEngine(ChannelDrivenFakeAudio audio, IStreamingTranscriber streamer)
    {
        var transcriber = new Transcriber();
        var configStore = new AutoDegradeTests.InMemoryConfigStore();
        var config = new AppConfig { StreamingMode = "auto" };
        configStore.Save(config);
        return new DictationEngine(
            audio, transcriber, new AutoDegradeTests.NoopPaste(), new AutoDegradeTests.NoopNotify(),
            configStore, config, log: null, history: null, streamer: streamer,
            specs: new CapableSpecs(), keyboardLayout: null);
    }
}
