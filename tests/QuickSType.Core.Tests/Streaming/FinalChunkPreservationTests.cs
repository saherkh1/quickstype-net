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
        var streaming = new TaskCompletionSource<bool>();
        engine.StateChanged += s => { if (s == DictationState.Idle) idle.TrySetResult(true); };
        engine.StateChanged += s => { if (s == DictationState.Streaming) streaming.TrySetResult(true); };

        engine.OnHotkeyPressed();
        engine.EffectiveMode.ShouldBe("streaming");
        var streamingWinner = await Task.WhenAny(streaming.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        streamingWinner.ShouldBe(streaming.Task, "Engine did not enter Streaming");

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
        var streaming = new TaskCompletionSource<bool>();
        engine.StateChanged += s => { if (s == DictationState.Idle) idle.TrySetResult(true); };
        engine.StateChanged += s => { if (s == DictationState.Streaming) streaming.TrySetResult(true); };

        engine.OnHotkeyPressed();
        var streamingWinner = await Task.WhenAny(streaming.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        streamingWinner.ShouldBe(streaming.Task, "Engine did not enter Streaming");
        audio.PushFrames(new ReadOnlyMemory<float>(new float[16000]));
        engine.OnHotkeyReleased();

        var winner = await Task.WhenAny(idle.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        winner.ShouldBe(idle.Task, "Engine did not reach Idle within 8s");

        injector.Applied.ShouldBeEmpty();
        paste.Pasted.ShouldBe(["hello world"]);
    }

    [Fact]
    public async Task Streaming_loads_language_specific_model_before_processing()
    {
        var streamer = new FinalYieldingStreamer(new TranscriptUpdate(0, "shalom"));
        var audio = new ChannelDrivenFakeAudio();
        var cfg = new AppConfig
        {
            StreamingMode = "auto",
            AutoLanguage = false,
            ActiveLanguage = "he",
            LanguageModels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["he"] = "ggml-medium-he",
            },
        };
        var engine = NewEngine(audio, streamer, cfg);
        var idle = new TaskCompletionSource<bool>();
        var streaming = new TaskCompletionSource<bool>();
        engine.StateChanged += s => { if (s == DictationState.Idle) idle.TrySetResult(true); };
        engine.StateChanged += s => { if (s == DictationState.Streaming) streaming.TrySetResult(true); };

        engine.OnHotkeyPressed();
        var streamingWinner = await Task.WhenAny(streaming.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        streamingWinner.ShouldBe(streaming.Task, "Engine did not enter Streaming after loading the language model");
        audio.PushFrames(new ReadOnlyMemory<float>(new float[16000]));
        engine.OnHotkeyReleased();

        var winner = await Task.WhenAny(idle.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        winner.ShouldBe(idle.Task, "Engine did not reach Idle within 8s");
        streamer.LoadedModelPath.ShouldBe(ModelCatalog.PathFor("ggml-medium-he"));
    }

    [Fact]
    public async Task Immediate_release_after_streaming_press_stops_audio_and_returns_idle()
    {
        var streamer = new FinalYieldingStreamer(new TranscriptUpdate(0, "ignored"));
        var audio = new ChannelDrivenFakeAudio();
        var engine = NewEngine(audio, streamer);
        var idle = new TaskCompletionSource<bool>();
        engine.StateChanged += s => { if (s == DictationState.Idle) idle.TrySetResult(true); };

        engine.OnHotkeyPressed();
        engine.State.ShouldBe(DictationState.LoadingModel);

        engine.OnHotkeyReleased();

        audio.IsRecording.ShouldBeFalse("release during pre-streaming LoadingModel must stop capture");
        var winner = await Task.WhenAny(idle.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        winner.ShouldBe(idle.Task, "Engine did not return to Idle after immediate release");
    }

    [Fact]
    public async Task Release_during_streaming_model_load_cancels_without_waiting_for_loader_to_finish()
    {
        var streamer = new BlockingLoadStreamer();
        var audio = new ChannelDrivenFakeAudio();
        var engine = NewEngine(audio, streamer);
        var idle = new TaskCompletionSource<bool>();
        engine.StateChanged += s => { if (s == DictationState.Idle) idle.TrySetResult(true); };

        engine.OnHotkeyPressed();
        var loadStarted = await Task.WhenAny(streamer.LoadStarted.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        loadStarted.ShouldBe(streamer.LoadStarted.Task, "streaming loader did not start");

        engine.OnHotkeyReleased();

        audio.IsRecording.ShouldBeFalse("release during LoadingModel must stop capture");
        var winner = await Task.WhenAny(idle.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        winner.ShouldBe(idle.Task, "Engine waited for the blocked loader instead of cancelling LoadingModel");
        var cancelWinner = await Task.WhenAny(streamer.CancelObservedTask.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        cancelWinner.ShouldBe(streamer.CancelObservedTask.Task, "streaming loader did not observe cancellation");
        streamer.CancelObserved.ShouldBeTrue();
    }

    private sealed class FinalYieldingStreamer : IStreamingTranscriber
    {
        private readonly IReadOnlyList<TranscriptUpdate> _updates;

        public FinalYieldingStreamer(params TranscriptUpdate[] updates)
        {
            _updates = updates.Length == 0 ? [new TranscriptUpdate(0, "final-chunk")] : updates;
        }

        public event Action? DegradeRequested;
        public string? LoadedModelPath { get; private set; }
        public bool? LoadedUseGpu { get; private set; }
        public void EnsureLoaded(string modelPath, bool useGpu = true, CancellationToken cancellationToken = default)
        {
            LoadedModelPath = modelPath;
            LoadedUseGpu = useGpu;
        }

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

    private sealed class BlockingLoadStreamer : IStreamingTranscriber
    {
        public TaskCompletionSource<bool> LoadStarted { get; } = new();
        public TaskCompletionSource<bool> CancelObservedTask { get; } = new();
        public bool CancelObserved { get; private set; }

        public event Action? DegradeRequested;

        public void EnsureLoaded(string modelPath, bool useGpu = true, CancellationToken cancellationToken = default)
        {
            _ = DegradeRequested;
            LoadStarted.TrySetResult(true);
            try
            {
                cancellationToken.WaitHandle.WaitOne();
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                CancelObserved = true;
                CancelObservedTask.TrySetResult(true);
                throw;
            }
        }

        public async IAsyncEnumerable<TranscriptUpdate> RunAsync(
            ChannelReader<ReadOnlyMemory<float>> frames,
            int sampleRate,
            AppConfig config,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var _ in frames.ReadAllAsync(ct).ConfigureAwait(false)) { }
            yield break;
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
