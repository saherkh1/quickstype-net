using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickSType.Core;
using QuickSType.Core.Audio;
using QuickSType.Core.Config;
using QuickSType.Core.Hotkey;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;
using SharpHook.Data;

namespace QuickSType.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    public const string AutoLanguageCode = "__auto";

    private readonly AppHost _host;
    private bool _suppressLanguageSync;

    [ObservableProperty] private string _hotkeyDisplay;
    [ObservableProperty] private bool _isCapturingHotkey;
    [ObservableProperty] private string _selectedModel;
    [ObservableProperty] private string _selectedLanguageCode = AutoLanguageCode;
    [ObservableProperty] private string? _selectedAudioDevice;
    [ObservableProperty] private string _transcriptionBackend;
    [ObservableProperty] private bool _showNotifications;
    [ObservableProperty] private bool _startAtLogin;
    [ObservableProperty] private string _testTranscriptionResult = string.Empty;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private double _modelDownloadPercent;
    [ObservableProperty] private string _modelDownloadStatus = string.Empty;

    public ObservableCollection<ModelInfo> AvailableModels { get; } = new(ModelCatalog.All);
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; }
    public ObservableCollection<LanguageRow> AvailableLanguages { get; }
    public ObservableCollection<string> Backends { get; } = new() { "auto", "cpu", "metal", "cuda" };

    public SettingsViewModel(AppHost host)
    {
        _host = host;
        var c = host.Config;
        _selectedModel = c.Model;
        _hotkeyDisplay = HotkeyService.Format(HotkeyService.ParseKey(c.Hotkey));
        _selectedAudioDevice = c.SelectedAudioDevice;
        _transcriptionBackend = c.TranscriptionBackend;
        _showNotifications = c.ShowNotifications;
        _startAtLogin = host.AutoLaunch.IsEnabled();
        _selectedLanguageCode = ResolveLanguageCode(c);

        AudioDevices = new ObservableCollection<AudioDeviceInfo>(host.Audio.ListInputDevices());
        AvailableLanguages = new ObservableCollection<LanguageRow>(
            Languages.Common.Select(l => new LanguageRow(l.Code, l.DisplayName, l.NativeName)));

        host.ConfigChanged += OnConfigChanged;
    }

    private static string ResolveLanguageCode(AppConfig c) =>
        c.AutoLanguage ? AutoLanguageCode : c.ActiveLanguage;

    private void OnConfigChanged(AppConfig cfg)
    {
        _suppressLanguageSync = true;
        try { SelectedLanguageCode = ResolveLanguageCode(cfg); }
        finally { _suppressLanguageSync = false; }
    }

    partial void OnSelectedLanguageCodeChanged(string value)
    {
        if (_suppressLanguageSync) return;
        if (value == AutoLanguageCode)
        {
            _host.UpdateConfig(_host.Config.WithAutoLanguage());
        }
        else
        {
            _host.UpdateConfig(_host.Config.WithLanguage(value));
        }
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
    private void SaveModel()
    {
        Save(c => c with { Model = SelectedModel });
    }

    [RelayCommand]
    private async Task DownloadSelectedModel(CancellationToken ct)
    {
        var model = ModelCatalog.Find(SelectedModel);
        if (model is null) return;
        ModelDownloadStatus = "Downloading…";
        var progress = new Progress<ModelDownloader.Progress>(p =>
        {
            ModelDownloadPercent = p.Percent;
            var mb = p.DownloadedBytes / 1_000_000.0;
            var totalMb = p.TotalBytes / 1_000_000.0;
            var speed = p.BytesPerSecond / 1_000_000.0;
            ModelDownloadStatus = $"{mb:F1} / {totalMb:F1} MB ({speed:F1} MB/s)";
        });
        try
        {
            await _host.ModelDownloader.DownloadAsync(model, progress, ct);
            ModelDownloadStatus = "Downloaded.";
        }
        catch (Exception ex)
        {
            ModelDownloadStatus = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveAudioDevice()
    {
        Save(c => c with { SelectedAudioDevice = SelectedAudioDevice });
    }

    [RelayCommand]
    private void SaveBackend()
    {
        Save(c => c with { TranscriptionBackend = TranscriptionBackend });
    }

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

public sealed record LanguageRow(string Code, string DisplayName, string NativeName)
{
    public string Label => $"{NativeName}  ({DisplayName})";
}
