using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    Recording,
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
    private AppConfig _config;
    private DictationState _state = DictationState.Idle;
    private CancellationTokenSource? _cts;

    public event Action<DictationState>? StateChanged;
    public event Action<string>? Transcribed;
    public event Action<Exception>? Errored;

    public DictationState State => _state;
    public AppConfig Config => _config;

    public DictationEngine(
        IAudioCapture audio,
        Transcriber transcriber,
        IPasteService paste,
        INotificationService notify,
        ConfigStore configStore,
        AppConfig config,
        ILogger<DictationEngine>? log = null,
        IHistoryService? history = null)
    {
        _audio = audio;
        _transcriber = transcriber;
        _paste = paste;
        _notify = notify;
        _history = history;
        _configStore = configStore;
        _config = config;
        _log = (ILogger?)log ?? NullLogger.Instance;

        _audio.SelectInputDevice(_config.SelectedAudioDevice);
    }

    public void UpdateConfig(AppConfig cfg)
    {
        _config = cfg;
        _audio.SelectInputDevice(cfg.SelectedAudioDevice);
        _configStore.Save(cfg);
    }

    public void OnHotkeyPressed()
    {
        if (_state != DictationState.Idle)
        {
            _log.LogDebug("Press ignored; state is {State}", _state);
            return;
        }

        try
        {
            _audio.Start();
            SetState(DictationState.Recording);
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

            var modelPath = ModelCatalog.PathFor(_config.Model);
            if (!File.Exists(modelPath))
            {
                _log.LogError("Model not installed: {Path}", modelPath);
                Errored?.Invoke(new FileNotFoundException("Whisper model not installed.", modelPath));
                SetState(DictationState.Idle);
                return;
            }

            var useGpu = _config.TranscriptionBackend != "cpu";
            _transcriber.EnsureLoaded(modelPath, useGpu);

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
        _cts?.Cancel();
        _cts?.Dispose();
        _audio.Dispose();
        _transcriber.Dispose();
    }
}
