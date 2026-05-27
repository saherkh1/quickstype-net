using System.Runtime.CompilerServices;
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

public class AutoDegradeTests
{
    private sealed class FakeStreamer : IStreamingTranscriber
    {
        public event Action? DegradeRequested;
        public void RaiseDegrade() => DegradeRequested?.Invoke();
        public void EnsureLoaded(string modelPath, bool useGpu = true, CancellationToken cancellationToken = default) { }
        public async IAsyncEnumerable<TranscriptUpdate> RunAsync(
            ChannelReader<ReadOnlyMemory<float>> frames, int sampleRate, AppConfig config,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield break;
        }
        public void Dispose() { }
    }

    internal sealed class CapableSpecs : ISystemSpecsService
    {
        public SystemSpecs Detect() => new(16.0, 100.0, false, HardwareTier.AppleSilicon);
        public ModelInfo RecommendModel(SystemSpecs s) => ModelCatalog.Default;
        public string? GetHardwareWarning(ModelInfo m, SystemSpecs s) => null;
        public bool IsBlocker(SystemSpecs s) => false;
        public bool IsStreamingCapable() => true;
        public double MinRamGb => 2.0;
        public double MinFreeDiskGb => 1.0;
    }

    [Fact]
    public void Degrade_request_flips_effective_mode_to_commit_on_pause()
    {
        var streamer = new FakeStreamer();
        var engine = NewEngine(streamer, new CapableSpecs(), streamingMode: "auto");

        engine.ResolveEffectiveMode().ShouldBe("streaming");

        streamer.RaiseDegrade();

        engine.EffectiveMode.ShouldBe("commit-on-pause");
        engine.ResolveEffectiveMode().ShouldBe("commit-on-pause");
    }

    [Fact]
    public void Mode_changed_event_fires_on_degrade()
    {
        var streamer = new FakeStreamer();
        var engine = NewEngine(streamer, new CapableSpecs(), streamingMode: "auto");
        var seen = new List<string>();
        engine.ModeChanged += s => seen.Add(s);

        streamer.RaiseDegrade();

        seen.ShouldContain("commit-on-pause");
    }

    [Fact]
    public void Changing_streaming_mode_to_streaming_clears_degraded_flag()
    {
        var streamer = new FakeStreamer();
        var engine = NewEngine(streamer, new CapableSpecs(), streamingMode: "auto");
        streamer.RaiseDegrade();
        engine.ResolveEffectiveMode().ShouldBe("commit-on-pause");

        engine.UpdateConfig(engine.Config with { StreamingMode = "streaming" });

        engine.ResolveEffectiveMode().ShouldBe("streaming");
    }

    internal static DictationEngine NewEngine(
        IStreamingTranscriber? streamer, ISystemSpecsService? specs, string streamingMode,
        IKeyboardLayoutService? keyboardLayout = null, AppConfig? config = null)
    {
        var audio = new FakeAudio();
        var transcriber = new Transcriber();
        var configStore = new InMemoryConfigStore();
        var cfg = config ?? new AppConfig { StreamingMode = streamingMode };
        configStore.Save(cfg);
        return new DictationEngine(
            audio, transcriber, new NoopPaste(), new NoopNotify(), configStore, cfg,
            log: null, history: null, streamer: streamer, specs: specs, keyboardLayout: keyboardLayout);
    }

    internal sealed class FakeAudio : IAudioCapture
    {
        public int SampleRate => 16000;
        public int Channels => 1;
        public bool IsRecording => false;
        public ChannelReader<ReadOnlyMemory<float>>? Frames => null;
        public void Start() { }
        public float[] Stop() => Array.Empty<float>();
        public IReadOnlyList<AudioDeviceInfo> ListInputDevices() => Array.Empty<AudioDeviceInfo>();
        public void SelectInputDevice(string? deviceName) { }
#pragma warning disable CS0067
        public event Action<float>? LevelChanged;
#pragma warning restore CS0067
        public void Dispose() { }
    }

    internal sealed class NoopPaste : IPasteService
    {
        public Task PasteAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class NoopNotify : INotificationService
    {
        public void Notify(string title, string message, string? subtitle = null) { }
        public Task PlayStartAsync() => Task.CompletedTask;
        public Task PlayStopAsync() => Task.CompletedTask;
    }

    internal sealed class InMemoryConfigStore : ConfigStore
    {
        private AppConfig _cfg = new();
        public InMemoryConfigStore() : base(null, Path.Combine(Path.GetTempPath(), $"qs-test-{Guid.NewGuid():N}.json")) { }
        public new AppConfig Load() => _cfg;
        public new void Save(AppConfig cfg) { _cfg = cfg; }
    }
}
