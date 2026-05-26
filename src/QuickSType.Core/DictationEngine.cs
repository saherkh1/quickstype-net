using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using QuickSType.Core.Audio;
using QuickSType.Core.Config;
using QuickSType.Core.Hotkey;
using QuickSType.Core.Paste;
using QuickSType.Core.Platform;
using QuickSType.Core.History;
using QuickSType.Core.Transcribe;

namespace QuickSType.Core;

public enum DictationState
{
    Idle,
    LoadingModel,   // model swap in progress; HUD visible; hotkey release cancels
    Recording,
    Streaming,
    Processing,
}

public sealed class DictationEngine : IDisposable
{
    private readonly ILogger _log;
    private readonly IAudioCapture _audio;
    private readonly Transcriber _transcriber;
    private readonly IPasteService _paste;
    private readonly INotificationService _notify;
    private readonly IHistoryService? _history;
    private readonly ConfigStore _configStore;
    private readonly IStreamingTranscriber? _streamer;
    private readonly ISystemSpecsService _specs;
    private readonly IKeyboardLayoutService? _keyboardLayout;
    private readonly IIncrementalInjector? _incrementalInjector;
    private AppConfig _config;
    private DictationState _state = DictationState.Idle;
    private CancellationTokenSource? _cts;
    private bool _degradedThisSession;
    private string _effectiveMode = "auto";
    private Task? _streamLoopTask;

    public event Action<DictationState>? StateChanged;
    public event Action<string>? Transcribed;
    public event Action<Exception>? Errored;
    public event Action<TranscriptUpdate>? TranscriptUpdate;
    public event Action<string>? ModeChanged;

    public DictationState State => _state;
    public AppConfig Config => _config;
    public string EffectiveMode => _effectiveMode;

    public DictationEngine(
        IAudioCapture audio,
        Transcriber transcriber,
        IPasteService paste,
        INotificationService notify,
        ConfigStore configStore,
        AppConfig config,
        ILogger<DictationEngine>? log = null,
        IHistoryService? history = null,
        IStreamingTranscriber? streamer = null,
        ISystemSpecsService? specs = null,
        IKeyboardLayoutService? keyboardLayout = null,
        IIncrementalInjector? incrementalInjector = null)
    {
        _audio = audio;
        _transcriber = transcriber;
        _paste = paste;
        _notify = notify;
        _history = history;
        _configStore = configStore;
        _config = config;
        _log = (ILogger?)log ?? NullLogger.Instance;
        _streamer = streamer;
        _specs = specs ?? new NullSystemSpecs();
        _keyboardLayout = keyboardLayout;
        _incrementalInjector = incrementalInjector;

        _audio.SelectInputDevice(_config.SelectedAudioDevice);

        if (_streamer is not null)
            _streamer.DegradeRequested += OnDegradeRequested;
    }

    public void UpdateConfig(AppConfig cfg)
    {
        var oldMode = _config.StreamingMode;
        _config = cfg;
        _audio.SelectInputDevice(cfg.SelectedAudioDevice);
        _configStore.Save(cfg);
        if (cfg.StreamingMode != oldMode && cfg.StreamingMode != "commit-on-pause")
        {
            _degradedThisSession = false;
            _log.LogInformation("StreamingMode changed; cleared degraded-this-session flag");
        }
    }

    internal string ResolveEffectiveMode()
    {
        if (_config.StreamingMode == "commit-on-pause") return "commit-on-pause";
        if (_degradedThisSession) return "commit-on-pause";
        if (_config.StreamingMode == "streaming") return "streaming";
        return _specs.IsStreamingCapable() ? "streaming" : "commit-on-pause";
    }

    /// <summary>
    /// Resolves the Whisper model ID to use for the current dictation.
    /// D-09: lazy — called at dictation start, not in advance.
    /// D-12: AutoLanguage bypasses per-language map entirely.
    /// T-08-04: validates the assigned id against the catalog to prevent path traversal / stale-config crash.
    /// </summary>
    internal static string ResolveModelId(AppConfig cfg)
    {
        // D-12: AutoLanguage = global model, no per-language map
        if (cfg.AutoLanguage) return cfg.Model;

        if (cfg.LanguageModels.TryGetValue(cfg.ActiveLanguage, out var langModel)
            && !string.IsNullOrEmpty(langModel)
            && ModelCatalog.Find(langModel) is not null)   // T-08-04: validate against catalog
        {
            return langModel;
        }
        return cfg.Model;   // fallback to global
    }

    internal AppConfig ApplyLayoutLanguageHint(AppConfig cfg)
    {
        if (!cfg.KeyboardLayoutDriven || _keyboardLayout is null) return cfg;
        var code = _keyboardLayout.CurrentLayout.Code;
        var mapped = Languages.LayoutCodeToWhisperLang(code);
        if (string.IsNullOrEmpty(mapped)) return cfg;
        return cfg with { ActiveLanguage = mapped, AutoLanguage = false };
    }

    public void OnHotkeyPressed()
    {
        if (_state != DictationState.Idle)
        {
            _log.LogDebug("Press ignored; state is {State}", _state);
            return;
        }

        var effective = ResolveEffectiveMode();
        if (effective != _effectiveMode)
        {
            _effectiveMode = effective;
            try { ModeChanged?.Invoke(_effectiveMode); }
            catch (Exception ex) { _log.LogError(ex, "ModeChanged handler threw"); }
        }

        try
        {
            _audio.Start();
            if (effective == "streaming" && _streamer is not null && _audio.Frames is { } frames)
            {
                SetState(DictationState.Streaming);
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var snapshot = ApplyLayoutLanguageHint(_config);
                _streamLoopTask = StreamLoopAsync(frames, _audio.SampleRate, snapshot, _cts.Token);
            }
            else
            {
                SetState(DictationState.Recording);
            }
            _ = _notify.PlayStartAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to start recording");
            Errored?.Invoke(ex);
        }
    }

    public void OnHotkeyReleased()
    {
        // D-11: hotkey release during model load cancels via the ct path and returns to Idle.
        // The mic was started in OnHotkeyPressed; stop it here so the next press can re-Start cleanly.
        if (_state == DictationState.LoadingModel)
        {
            try { _audio.Stop(); }
            catch (Exception ex) { _log.LogError(ex, "Stop on LoadingModel release threw"); }
            _cts?.Cancel();
            return;
        }

        if (_state == DictationState.Streaming)
        {
            // B3: complete channel via _audio.Stop() — NOT _cts.Cancel()
            try { _audio.Stop(); }
            catch (Exception ex) { _log.LogError(ex, "Stop on streaming release threw"); }
            _ = _notify.PlayStopAsync();
            SetState(DictationState.Processing);
            _ = AwaitStreamCompletionAsync();
            return;
        }

        if (_state != DictationState.Recording)
        {
            _log.LogDebug("Release ignored; state is {State}", _state);
            return;
        }

        var samples = _audio.Stop();
        _ = _notify.PlayStopAsync();
        SetState(DictationState.Processing);

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = ProcessAsync(samples, _cts.Token);
    }

    private async Task StreamLoopAsync(
        System.Threading.Channels.ChannelReader<ReadOnlyMemory<float>> frames,
        int sampleRate,
        AppConfig snapshot,
        CancellationToken ct)
    {
        try
        {
            var streamedText = snapshot.EnableStreamingInsertion ? null : new StringBuilder();
            await foreach (var update in _streamer!.RunAsync(frames, sampleRate, snapshot, ct).ConfigureAwait(false))
            {
                if (snapshot.EnableStreamingInsertion && _incrementalInjector is not null)
                {
                    try { await _incrementalInjector.ApplyAsync(update, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { _log.LogError(ex, "IncrementalInjector.ApplyAsync threw"); }
                }
                if (!snapshot.EnableStreamingInsertion)
                {
                    ApplyTranscriptUpdate(streamedText!, update);
                }
                try { TranscriptUpdate?.Invoke(update); }
                catch (Exception ex) { _log.LogError(ex, "TranscriptUpdate handler threw"); }
            }

            if (streamedText is not null)
            {
                await CompleteDeferredStreamingPasteAsync(streamedText.ToString(), snapshot, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* expected on timeout-cancel fallback */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "Streaming loop failed");
            Errored?.Invoke(ex);
        }
    }

    private async Task CompleteDeferredStreamingPasteAsync(string text, AppConfig snapshot, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _log.LogInformation("Empty streaming transcription; nothing to paste");
            return;
        }

        await _paste.PasteAsync(text, ct).ConfigureAwait(false);
        Transcribed?.Invoke(text);

        if (_history is not null)
        {
            var entry = new HistoryEntry
            {
                Ts = DateTime.UtcNow.ToString("o"),
                Text = text,
                Model = snapshot.Model,
                Lang = snapshot.ActiveLanguage,
                DurationMs = 0,
                Device = snapshot.SelectedAudioDevice,
            };
            _ = _history.AppendAsync(entry);
        }

        if (snapshot.ShowNotifications)
        {
            _notify.Notify("QuickSType", text.Length > 80 ? text[..80] + "…" : text);
        }
    }

    private static void ApplyTranscriptUpdate(StringBuilder text, TranscriptUpdate update)
    {
        if (update.RetractChars > 0)
        {
            var count = Math.Min(update.RetractChars, text.Length);
            text.Remove(text.Length - count, count);
        }

        if (!string.IsNullOrEmpty(update.AppendText))
        {
            text.Append(update.AppendText);
        }
    }

    private async Task AwaitStreamCompletionAsync()
    {
        var loop = _streamLoopTask;
        if (loop is null) { SetState(DictationState.Idle); return; }
        try
        {
            var winner = await Task.WhenAny(loop, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            if (winner != loop)
            {
                _log.LogWarning("Stream loop did not complete within timeout; cancelling");
                _cts?.Cancel();
                try { await loop.ConfigureAwait(false); } catch { /* observed */ }
            }
        }
        catch (Exception ex) { _log.LogError(ex, "AwaitStreamCompletionAsync threw"); }
        finally
        {
            _streamLoopTask = null;
            SetState(DictationState.Idle);
        }
    }

    private void OnDegradeRequested()
    {
        _degradedThisSession = true;
        var newMode = "commit-on-pause";
        if (newMode != _effectiveMode)
        {
            _effectiveMode = newMode;
            _log.LogWarning("Auto-degrade triggered; effective mode is now {Mode}", _effectiveMode);
            try { ModeChanged?.Invoke(_effectiveMode); }
            catch (Exception ex) { _log.LogError(ex, "ModeChanged handler threw"); }
        }
    }

    private async Task ProcessAsync(float[] samples, CancellationToken ct)
    {
        try
        {
            if (samples.Length < _audio.SampleRate / 5)
            {
                _log.LogInformation("Recording too short ({Samples} samples); discarding", samples.Length);
                SetState(DictationState.Idle);
                return;
            }

            var modelId = ResolveModelId(_config);
            var modelPath = ModelCatalog.PathFor(modelId);
            if (!File.Exists(modelPath))
            {
                _log.LogError("Model not installed: {Path}", modelPath);
                Errored?.Invoke(new FileNotFoundException("Whisper model not installed.", modelPath));
                SetState(DictationState.Idle);
                return;
            }

            var useGpu = _config.TranscriptionBackend != "cpu";
            if (_transcriber.LoadedModelPath != modelPath)
            {
                SetState(DictationState.LoadingModel);
                // D-11: pass ct into EnsureLoaded so cancellation is cooperative — the
                // ThrowIfCancellationRequested checks inside the lock terminate the task
                // before the next call proceeds. This avoids the race where the abandoned
                // task (Task.Run + WaitAsync) could finish loading the wrong factory while
                // a new press is already attempting to swap models.
                await Task.Run(() => _transcriber.EnsureLoaded(modelPath, useGpu, ct), ct);
            }

            var text = await _transcriber.TranscribeAsync(samples, _audio.SampleRate, _config, ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                _log.LogInformation("Empty transcription; nothing to paste");
                SetState(DictationState.Idle);
                return;
            }

            await _paste.PasteAsync(text, ct);
            Transcribed?.Invoke(text);

            // CONFIG-05: append a history entry after successful paste. Fire-and-forget;
            // HistoryService.AppendAsync swallows I/O errors internally.
            if (_history is not null)
            {
                var durationMs = _audio.SampleRate > 0
                    ? (int)Math.Round(samples.Length / (double)_audio.SampleRate * 1000.0)
                    : 0;
                var entry = new HistoryEntry
                {
                    Ts = DateTime.UtcNow.ToString("o"),
                    Text = text,
                    Model = _config.Model,
                    Lang = _config.ActiveLanguage,
                    DurationMs = durationMs,
                    Device = _config.SelectedAudioDevice,
                };
                _ = _history.AppendAsync(entry);
            }

            if (_config.ShowNotifications)
            {
                _notify.Notify("QuickSType", text.Length > 80 ? text[..80] + "…" : text);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Dictation pipeline failed");
            Errored?.Invoke(ex);
        }
        finally
        {
            SetState(DictationState.Idle);
        }
    }

    private void SetState(DictationState s)
    {
        if (_state == s) return;
        _state = s;
        try { StateChanged?.Invoke(s); }
        catch (Exception ex) { _log.LogError(ex, "StateChanged handler threw"); }
    }

    public void Dispose()
    {
        if (_streamer is not null) _streamer.DegradeRequested -= OnDegradeRequested;
        _streamer?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _audio.Dispose();
        _transcriber.Dispose();
    }

    // Null implementation — used when no ISystemSpecsService is injected.
    private sealed class NullSystemSpecs : ISystemSpecsService
    {
        public SystemSpecs Detect() => new(0.0, 0.0, false, HardwareTier.LowResource);
        public ModelInfo RecommendModel(SystemSpecs specs) => ModelCatalog.Default;
        public string? GetHardwareWarning(ModelInfo model, SystemSpecs specs) => null;
        public bool IsBlocker(SystemSpecs specs) => false;
        public bool IsStreamingCapable() => false;
        public double MinRamGb => 2.0;
        public double MinFreeDiskGb => 1.0;
    }
}
