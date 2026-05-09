using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickSType.Core;
using QuickSType.Core.Audio;
using QuickSType.Core.Config;
using QuickSType.Core.Hotkey;
using QuickSType.Core.Platform;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;

namespace QuickSType.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly ILogger _log;
    private bool _suppressLanguageSync;

    [ObservableProperty] private string _hotkeyDisplay;
    [ObservableProperty] private bool _isCapturingHotkey;
    [ObservableProperty] private string _selectedModel;
    [ObservableProperty] private bool _autoLanguage;
    [ObservableProperty] private string _activeLanguageDisplay = string.Empty;
    [ObservableProperty] private string? _selectedAudioDevice;
    [ObservableProperty] private string _transcriptionBackend;
    [ObservableProperty] private bool _showNotifications;
    [ObservableProperty] private bool _startAtLogin;
    [ObservableProperty] private string _testTranscriptionResult = string.Empty;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private double _modelDownloadPercent;
    [ObservableProperty] private string _modelDownloadStatus = string.Empty;
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private string _firstRunBannerBody = string.Empty;
    [ObservableProperty] private string _firstRunBannerCaption = "You can change this any time in Settings.";

    public ObservableCollection<ModelRowViewModel> AvailableModels { get; }
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; }
    public ObservableCollection<LanguageRow> AvailableLanguages { get; }
    public ObservableCollection<string> Backends { get; } = new() { "auto", "cpu", "metal", "cuda" };

    public SettingsViewModel(AppHost host)
    {
        _host = host;
        _log = host.LoggerFactory.CreateLogger<SettingsViewModel>();
        var c = host.Config;

        // First-run model selection (D-01): if PreferredModel is null, override the loaded Model
        // with the hardware-recommended one so the user sees it pre-selected in the ComboBox.
        if (c.PreferredModel is null)
        {
            var recommended = host.SystemSpecsService.RecommendModel(host.SystemSpecs);
            _selectedModel = recommended.Id;
            _isFirstRun = true;

            var sizeMb = (int)Math.Round(recommended.ApproxSizeBytes / 1_000_000.0 / 10.0) * 10;
            var tierLabel = host.SystemSpecs.Tier switch
            {
                HardwareTier.AppleSilicon => "Apple Silicon",
                HardwareTier.WindowsCuda  => "Windows with CUDA",
                HardwareTier.Ram16Plus    => "16 GB+ RAM",
                HardwareTier.Ram8To16     => "8–16 GB RAM",
                _                          => "low-resource fallback",
            };
            _firstRunBannerBody =
                $"We recommend {recommended.DisplayName} for your system ({tierLabel}). " +
                $"It's pre-selected below. Click Download to fetch it (~{sizeMb} MB).";

            _log.LogInformation("First run: recommending {ModelId} for {Tier}",
                recommended.Id, host.SystemSpecs.Tier);
        }
        else
        {
            _selectedModel = c.PreferredModel;
            _isFirstRun = false;
        }

        AvailableModels = new ObservableCollection<ModelRowViewModel>(
            ModelCatalog.All.Select(m => new ModelRowViewModel(m, host.SystemSpecs, host.SystemSpecsService)));

        _hotkeyDisplay = HotkeyService.Format(HotkeyService.ParseKey(c.Hotkey));
        _autoLanguage = c.AutoLanguage;
        _selectedAudioDevice = c.SelectedAudioDevice;
        _transcriptionBackend = c.TranscriptionBackend;
        _showNotifications = c.ShowNotifications;
        _startAtLogin = host.AutoLaunch.IsEnabled();

        AudioDevices = new ObservableCollection<AudioDeviceInfo>(host.Audio.ListInputDevices());

        AvailableLanguages = new ObservableCollection<LanguageRow>(
            Languages.Common.Select(l => new LanguageRow(
                l.Code, l.DisplayName, l.NativeName,
                isEnabled: c.Languages.Contains(l.Code, StringComparer.OrdinalIgnoreCase))));

        foreach (var row in AvailableLanguages)
            row.PropertyChanged += OnLanguageRowChanged;

        UpdateActiveLanguageDisplay(c);
        host.ConfigChanged += OnConfigChanged;
    }

    private void OnConfigChanged(AppConfig cfg)
    {
        _suppressLanguageSync = true;
        try
        {
            AutoLanguage = cfg.AutoLanguage;
            var enabled = new HashSet<string>(cfg.Languages, StringComparer.OrdinalIgnoreCase);
            foreach (var row in AvailableLanguages)
            {
                row.IsEnabled = enabled.Contains(row.Code);
            }
            UpdateActiveLanguageDisplay(cfg);
        }
        finally { _suppressLanguageSync = false; }
    }

    private void UpdateActiveLanguageDisplay(AppConfig c)
    {
        if (c.AutoLanguage)
        {
            ActiveLanguageDisplay = "Auto-detect";
            return;
        }
        var info = Languages.Find(c.ActiveLanguage);
        ActiveLanguageDisplay = info is null
            ? c.ActiveLanguage
            : $"{info.NativeName} ({info.DisplayName})";
    }

    partial void OnAutoLanguageChanged(bool value)
    {
        if (_suppressLanguageSync) return;
        _host.UpdateConfig(_host.Config.WithAutoLanguage(value));
    }

    private void OnLanguageRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressLanguageSync) return;
        if (e.PropertyName != nameof(LanguageRow.IsEnabled)) return;

        var enabled = AvailableLanguages.Where(l => l.IsEnabled).Select(l => l.Code).ToList();
        _host.UpdateConfig(_host.Config.WithEnabledLanguages(enabled));
    }

    [RelayCommand]
    private async Task CaptureHotkey()
    {
        IsCapturingHotkey = true;
        HotkeyDisplay = "Press a key…";
        try
        {
            var key = await _host.Hotkey.CaptureNextKeyAsync(TimeSpan.FromSeconds(5));
            HotkeyDisplay = HotkeyService.Format(key);
            Save(c => c with { Hotkey = HotkeyDisplay });
        }
        catch (TaskCanceledException)
        {
            HotkeyDisplay = HotkeyService.Format(HotkeyService.ParseKey(_host.Config.Hotkey));
        }
        finally
        {
            IsCapturingHotkey = false;
        }
    }

    [RelayCommand]
    private void SaveModel() => Save(c => c with { Model = SelectedModel, PreferredModel = SelectedModel });

    [RelayCommand]
    private async Task DownloadSelectedModel(CancellationToken ct)
    {
        var model = ModelCatalog.Find(SelectedModel);
        if (model is null) return;
        ModelDownloadStatus = "Starting download…";
        var progress = new Progress<ModelDownloader.Progress>(p =>
        {
            ModelDownloadPercent = p.Percent;
            var mb = p.DownloadedBytes / 1_000_000.0;
            var totalMb = p.TotalBytes / 1_000_000.0;
            var speed = p.BytesPerSecond / 1_000_000.0;
            ModelDownloadStatus = $"Downloading… {p.Percent:F0}% ({mb:F1} / {totalMb:F1} MB, {speed:F1} MB/s)";
        });
        try
        {
            await _host.ModelDownloader.DownloadAsync(model, progress, ct);

            // ModelDownloader.DownloadAsync verifies SHA-256 internally (HARDEN-01).
            // Show the verifying state cosmetically (it has already happened on the previous line).
            ModelDownloadStatus = "Verifying SHA-256…";

            // D-02 + D-03: persist PreferredModel AND keep Model in sync, only after verify passes.
            _host.UpdateConfig(_host.Config.WithPreferredModel(SelectedModel) with { Model = SelectedModel });
            if (IsFirstRun)
            {
                IsFirstRun = false;
                _log.LogInformation("PreferredModel set to {ModelId}; first-run flow complete", SelectedModel);
            }

            ModelDownloadStatus = $"{model.DisplayName} ready.";
            _ = AutoClearStatusAsync($"{model.DisplayName} ready.", TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            ModelDownloadStatus = $"Error: {ex.Message}";
        }
    }

    private async Task AutoClearStatusAsync(string expected, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            if (ModelDownloadStatus == expected) ModelDownloadStatus = string.Empty;
        }
        catch { /* swallow on dispose */ }
    }

    [RelayCommand]
    private void SaveAudioDevice() => Save(c => c with { SelectedAudioDevice = SelectedAudioDevice });

    [RelayCommand]
    private void SaveBackend() => Save(c => c with { TranscriptionBackend = TranscriptionBackend });

    [RelayCommand]
    private void ToggleStartAtLogin()
    {
        StartAtLogin = !StartAtLogin;
        _host.AutoLaunch.SetEnabled(StartAtLogin);
        Save(c => c with { StartAtLogin = StartAtLogin });
    }

    [RelayCommand]
    private void ToggleNotifications()
    {
        ShowNotifications = !ShowNotifications;
        Save(c => c with { ShowNotifications = ShowNotifications });
    }

    [RelayCommand]
    private async Task TestTranscription()
    {
        IsTesting = true;
        TestTranscriptionResult = "Recording 3 s…";
        try
        {
            _host.Audio.Start();
            await Task.Delay(3000);
            var samples = _host.Audio.Stop();
            TestTranscriptionResult = "Transcribing…";
            var modelPath = ModelCatalog.PathFor(_host.Config.Model);
            if (!File.Exists(modelPath))
            {
                TestTranscriptionResult = $"Model not installed: {_host.Config.Model}";
                return;
            }
            _host.Transcriber.EnsureLoaded(modelPath);
            var text = await _host.Transcriber.TranscribeAsync(samples, _host.Audio.SampleRate, _host.Config);
            TestTranscriptionResult = string.IsNullOrWhiteSpace(text) ? "(no speech detected)" : text;
        }
        catch (Exception ex)
        {
            TestTranscriptionResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    private void Save(Func<AppConfig, AppConfig> update)
    {
        var newCfg = update(_host.Config);
        _host.UpdateConfig(newCfg);
    }
}

public sealed partial class LanguageRow : ObservableObject
{
    public string Code { get; }
    public string DisplayName { get; }
    public string NativeName { get; }

    [ObservableProperty] private bool _isEnabled;

    public string Label => $"{NativeName}  ({DisplayName})";

    public LanguageRow(string code, string displayName, string nativeName, bool isEnabled)
    {
        Code = code;
        DisplayName = displayName;
        NativeName = nativeName;
        IsEnabled = isEnabled;
    }
}
