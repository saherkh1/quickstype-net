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
using QuickSType.UI.Updates;

namespace QuickSType.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly ILogger _log;
    private bool _suppressLanguageSync;
    private bool _suppressTelemetrySync;

    [ObservableProperty] private string _hotkeyDisplay;
    [ObservableProperty] private bool _isCapturingHotkey;
    [ObservableProperty] private string _selectedModel;
    [ObservableProperty] private bool _autoLanguage;
    [ObservableProperty] private string _activeLanguageDisplay = string.Empty;
    [ObservableProperty] private string? _selectedAudioDevice;
    [ObservableProperty] private string _transcriptionBackend;
    [ObservableProperty] private string _streamingMode;
    [ObservableProperty] private string _appearanceSetting;
    [ObservableProperty] private bool _showNotifications;
    [ObservableProperty] private bool _enableCrashTelemetry;
    [ObservableProperty] private bool _startAtLogin;
    [ObservableProperty] private string _testTranscriptionResult = string.Empty;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private double _modelDownloadPercent;
    [ObservableProperty] private string _modelDownloadStatus = string.Empty;
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private string _firstRunBannerBody = string.Empty;
    [ObservableProperty] private string _firstRunBannerCaption = "You can change this any time in Settings.";
    [ObservableProperty] private string _currentVersionText = "Version unknown";
    [ObservableProperty] private string _updateChannelText = "Channel: canary";
    [ObservableProperty] private string _updateStatusText = "QuickSType is up to date.";
    [ObservableProperty] private string _updateActionText = "Check for Updates";
    [ObservableProperty] private string _lastUpdateCheckText = "Last checked: never";
    [ObservableProperty] private bool _isUpdateActionEnabled = true;
    [ObservableProperty] private bool _isCheckingForUpdates;
    [ObservableProperty] private bool _isPermissionBannerVisible;
    [ObservableProperty] private string _permissionBannerText = string.Empty;
    private UpdateCheckResult? _lastUpdateResult;
    private DateTimeOffset? _lastUpdateChecked;
    private PostUpdateSelfCheckResult _postUpdateSelfCheckResult = PostUpdateSelfCheckResult.None("unknown");

    public ObservableCollection<ModelRowViewModel> AvailableModels { get; }
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; }
    public ObservableCollection<LanguageRow> AvailableLanguages { get; }
    public ObservableCollection<string> Backends { get; } = new() { "auto", "cpu", "metal", "cuda" };
    public ObservableCollection<string> AppearanceSettings { get; } = new() { "Auto", "On", "Off" };
    public ObservableCollection<StreamingModeOption> StreamingModes { get; } = new()
    {
        new("auto", "Auto (recommended)"),
        new("streaming", "Always stream"),
        new("commit-on-pause", "Commit on pause"),
    };

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
        _streamingMode = c.StreamingMode;
        _appearanceSetting = c.EnableVibrancy switch
        {
            true => "On",
            false => "Off",
            null => "Auto",
        };
        _showNotifications = c.ShowNotifications;
        _enableCrashTelemetry = c.EnableCrashTelemetry;
        _startAtLogin = host.AutoLaunch.IsEnabled();

        AudioDevices = new ObservableCollection<AudioDeviceInfo>(host.Audio.ListInputDevices());

        AvailableLanguages = new ObservableCollection<LanguageRow>(
            Languages.Common.Select(l => new LanguageRow(
                l.Code, l.DisplayName, l.NativeName,
                isEnabled: c.Languages.Contains(l.Code, StringComparer.OrdinalIgnoreCase))));

        foreach (var row in AvailableLanguages)
            row.PropertyChanged += OnLanguageRowChanged;

        UpdateActiveLanguageDisplay(c);
        RefreshUpdateStatus(host.UpdateService.GetCurrentStatus(c));
        RefreshPostUpdateSelfCheck();
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
            _suppressTelemetrySync = true;
            try { EnableCrashTelemetry = cfg.EnableCrashTelemetry; }
            finally { _suppressTelemetrySync = false; }
            RefreshUpdateStatus(_host.UpdateService.GetCurrentStatus(cfg));
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

    partial void OnAppearanceSettingChanged(string value)
    {
        var vibrancy = value switch
        {
            "On" => (bool?)true,
            "Off" => (bool?)false,
            _ => (bool?)null,
        };
        Save(c => c with { EnableVibrancy = vibrancy });
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
    private void SaveMode() => Save(c => c with { StreamingMode = StreamingMode });

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

    partial void OnEnableCrashTelemetryChanged(bool value)
    {
        if (_suppressTelemetrySync) return;
        Save(c => c with { EnableCrashTelemetry = value });
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

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (IsCheckingForUpdates) return;

        var status = _lastUpdateResult?.Status ?? _host.UpdateService.GetCurrentStatus(_host.Config);
        if (_lastUpdateResult is not null
            && status.Kind is UpdateStatusKind.UpdateAvailable or UpdateStatusKind.UpdateReadyToRestart)
        {
            if (_host.Engine.State != DictationState.Idle)
            {
                RefreshUpdateStatus(status with { Message = "Stop recording before installing the update." });
                return;
            }

            try
            {
                _host.UpdateService.ApplyUpdatesAndRestart(_lastUpdateResult!);
            }
            catch (Exception ex)
            {
                RefreshUpdateStatus(new UpdateStatus(
                    UpdateStatusKind.Error,
                    status.CurrentVersion,
                    status.Channel,
                    Message: ex.Message));
            }
            return;
        }

        IsCheckingForUpdates = true;
        RefreshUpdateStatus(status);
        try
        {
            var result = await _host.UpdateService.CheckForUpdatesAsync(_host.Config);
            _lastUpdateChecked = DateTimeOffset.Now;
            if (result.Status.Kind == UpdateStatusKind.UpdateAvailable)
            {
                await _host.UpdateService.DownloadUpdatesAsync(result);
                result = result with
                {
                    Status = result.Status with
                    {
                        Kind = UpdateStatusKind.UpdateReadyToRestart,
                        Message = result.TargetVersion is null
                            ? "Update is ready to install."
                            : $"Update {result.TargetVersion} is ready to install.",
                    },
                };
            }
            _lastUpdateResult = result;
            RefreshUpdateStatus(result.Status);
        }
        catch (Exception ex)
        {
            RefreshUpdateStatus(new UpdateStatus(
                UpdateStatusKind.Error,
                status.CurrentVersion,
                status.Channel,
                Message: ex.Message));
        }
        finally
        {
            IsCheckingForUpdates = false;
            RefreshUpdateStatus(_lastUpdateResult?.Status ?? _host.UpdateService.GetCurrentStatus(_host.Config));
        }
    }

    partial void OnIsCheckingForUpdatesChanged(bool value)
    {
        RefreshUpdateStatus(_lastUpdateResult?.Status ?? _host.UpdateService.GetCurrentStatus(_host.Config));
    }

    private void RefreshUpdateStatus(UpdateStatus status)
    {
        var ui = UpdatePresentation.FromStatus(status, _lastUpdateChecked, IsCheckingForUpdates, _host.Engine.State);
        CurrentVersionText = UpdatePresentation.FormatVersion(status.CurrentVersion);
        UpdateChannelText = UpdatePresentation.FormatChannel(status.Channel);
        UpdateStatusText = ui.SettingsStatus;
        UpdateActionText = ui.ActionText;
        LastUpdateCheckText = ui.LastCheckedText;
        IsUpdateActionEnabled = ui.IsActionEnabled;
    }

    [RelayCommand]
    private void OpenAccessibilitySettings()
    {
        _host.Permissions.OpenAccessibilitySettings();
    }

    [RelayCommand]
    private void DismissPermissionBanner()
    {
        _host.PostUpdateSelfCheck.Dismiss(_postUpdateSelfCheckResult.Version);
        IsPermissionBannerVisible = false;
    }

    private void RefreshPostUpdateSelfCheck()
    {
        _postUpdateSelfCheckResult = _host.PostUpdateSelfCheck.Evaluate(_host.Permissions);
        IsPermissionBannerVisible = _postUpdateSelfCheckResult.Kind == PostUpdateSelfCheckKind.AccessibilityMissing;
        PermissionBannerText = _postUpdateSelfCheckResult.Message ?? string.Empty;
    }

    private void Save(Func<AppConfig, AppConfig> update)
    {
        var newCfg = update(_host.Config);
        _host.UpdateConfig(newCfg);
    }
}

public sealed record StreamingModeOption(string Value, string Display);

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
