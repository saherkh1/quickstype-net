using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using QuickSType.Core;
using QuickSType.Core.Transcribe;

namespace QuickSType.UI.ViewModels;

public sealed partial class HudViewModel : ObservableObject
{
    [ObservableProperty] private bool _isActive;       // true when Recording, Streaming, or LoadingModel
    [ObservableProperty] private bool _isRecording;    // true only when Recording (for blink animation)
    [ObservableProperty] private bool _isStreaming;    // true only when Streaming (for accent dot)
    [ObservableProperty] private bool _isLoadingModel; // true only when LoadingModel (model swap in progress)
    [ObservableProperty] private string _elapsedText = "0:00";
    [ObservableProperty] private string _transcriptPreview = string.Empty;
    [ObservableProperty] private bool _isVibrancyEnabled;

    private readonly DispatcherTimer _elapsedTimer;
    private DateTime _startedAt;
    private string _preview = string.Empty;

    public HudViewModel()
    {
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();
    }

    public void OnStateChanged(DictationState state)
    {
        IsActive = state is DictationState.Recording or DictationState.Streaming or DictationState.LoadingModel;
        IsRecording = state == DictationState.Recording;
        IsStreaming = state == DictationState.Streaming;
        IsLoadingModel = state == DictationState.LoadingModel;

        if (IsActive && !_elapsedTimer.IsEnabled)
        {
            _startedAt = DateTime.UtcNow;
            ElapsedText = "0:00";
            _elapsedTimer.Start();
        }
        else if (!IsActive)
        {
            _elapsedTimer.Stop();
            ElapsedText = "0:00";
            _preview = string.Empty;
            TranscriptPreview = string.Empty;
        }
    }

    public void OnTranscriptUpdate(TranscriptUpdate update)
    {
        if (update.RetractChars > 0)
            _preview = _preview.Length >= update.RetractChars
                ? _preview[..^update.RetractChars]
                : string.Empty;
        _preview += update.AppendText;
        // Truncate preview for display (max 60 chars to avoid overflow)
        TranscriptPreview = _preview.Length > 60 ? "…" + _preview[^59..] : _preview;
    }

    public void UpdateVibrancy(bool enabled)
    {
        IsVibrancyEnabled = enabled;
    }

    private void UpdateElapsed()
    {
        var elapsed = DateTime.UtcNow - _startedAt;
        ElapsedText = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
    }
}
