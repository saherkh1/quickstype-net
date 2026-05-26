using System.Threading.Channels;
using QuickSType.Core;
using QuickSType.Core.Audio;
using QuickSType.Core.Config;
using QuickSType.Core.Paste;
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

    [Fact]
    public async Task Disabled_streaming_insertion_pastes_final_text_after_release_without_incremental_injection()
    {
        var streamer = new FinalYieldingStreamer(
            new TranscriptUpdate(0, "hello "),
            new TranscriptUpdate(0, "world"));
        var audio = new ChannelDrivenFakeAudio();
        var paste = new RecordingPaste();
        var injector = new RecordingInjector();
        var engine = NewEngine(
            audio,
            streamer,
            new AppConfig { StreamingMode = "auto", EnableStreamingInsertion = false },
            paste,
            injector);
        var idle = new TaskCompletionSource<bool>();
        engine.StateChanged += s => { if (s == DictationState.Idle) idle.TrySetResult(true); };

        engine.OnHotkeyPressed();
        audio.PushFrames(new ReadOnlyMemory<float>(new float[16000]));
        engine.OnHotkeyReleased();

        var winner = await Task.WhenAny(idle.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        winner.ShouldBe(idle.Task, "Engine did not reach Idle within 8s");

        injector.Applied.ShouldBeEmpty();
        paste.Pasted.ShouldBe(["hello world"]);
    }

    private sealed class FinalYieldingStreamer : IStreamingTranscriber
    {
        private readonly IReadOnlyList<TranscriptUpdate> _updates;

        public FinalYieldingStreamer(params TranscriptUpdate[] updates)
        {
            _updates = updates.Length == 0 ? [new TranscriptUpdate(0, "final-chunk")] : updates;
        }

        public event Action? DegradeRequested;
        public async IAsyncEnumerable<TranscriptUpdate> RunAsync(
            ChannelReader<ReadOnlyMemory<float>> frames, int sampleRate, AppConfig config,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var _ in frames.ReadAllAsync(ct).ConfigureAwait(false)) { }
            _ = DegradeRequested;
            foreach (var update in _updates)
                yield return update;
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
#pragma warning disable CS0067
        public event Action<float>? LevelChanged;
#pragma warning restore CS0067
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

    private sealed class RecordingPaste : IPasteService
    {
        public List<string> Pasted { get; } = [];
        public Task PasteAsync(string text, CancellationToken cancellationToken = default)
        {
            Pasted.Add(text);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInjector : IIncrementalInjector
    {
        public List<TranscriptUpdate> Applied { get; } = [];
        public Task ApplyAsync(TranscriptUpdate update, CancellationToken cancellationToken = default)
        {
            Applied.Add(update);
            return Task.CompletedTask;
        }
    }

    private static DictationEngine NewEngine(
        ChannelDrivenFakeAudio audio,
        IStreamingTranscriber streamer,
        AppConfig? config = null,
        IPasteService? paste = null,
        IIncrementalInjector? injector = null)
    {
        var transcriber = new Transcriber();
        var configStore = new AutoDegradeTests.InMemoryConfigStore();
        var cfg = config ?? new AppConfig { StreamingMode = "auto" };
        configStore.Save(cfg);
        return new DictationEngine(
            audio, transcriber, paste ?? new AutoDegradeTests.NoopPaste(), new AutoDegradeTests.NoopNotify(),
            configStore, cfg, log: null, history: null, streamer: streamer,
            specs: new CapableSpecs(), keyboardLayout: null, incrementalInjector: injector);
    }
}
