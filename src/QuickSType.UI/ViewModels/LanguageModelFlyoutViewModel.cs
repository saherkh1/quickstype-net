using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;

namespace QuickSType.UI.ViewModels;

public sealed partial class LanguageModelFlyoutViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly ILogger _log;

    public string LangCode { get; }

    [ObservableProperty] private string _selectedModelId;
    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty] private string _downloadStatus = string.Empty;
    [ObservableProperty] private bool _isDownloading;

    public ObservableCollection<ModelInfo> AvailableLanguageModels { get; }
    public string GlobalModelId => _host.Config.Model;

    /// <summary>
    /// True when a language-specific model is selected AND that model is not yet installed on disk.
    /// Drives the visibility of the Download button in the flyout.
    /// </summary>
    public bool CanDownload =>
        !string.IsNullOrEmpty(SelectedModelId) && !ModelCatalog.IsInstalled(SelectedModelId);

    public LanguageModelFlyoutViewModel(string langCode, AppHost host)
    {
        LangCode = langCode;
        _host = host;
        _log = host.LoggerFactory.CreateLogger<LanguageModelFlyoutViewModel>();

        var langSpecific = ModelCatalog.LanguageModels.TryGetValue(langCode, out var entries)
            ? entries
            : Array.Empty<ModelInfo>();
        AvailableLanguageModels = new ObservableCollection<ModelInfo>(langSpecific);

        _host.Config.LanguageModels.TryGetValue(langCode, out var currentId);
        _selectedModelId = currentId ?? string.Empty;
    }

    partial void OnSelectedModelIdChanged(string value)
    {
        OnPropertyChanged(nameof(CanDownload));
    }

    [RelayCommand]
    private void SelectGlobalModel() => SelectedModelId = string.Empty;

    [RelayCommand]
    private async Task Download(CancellationToken ct)
    {
        var model = ModelCatalog.Find(SelectedModelId);
        if (model is null) return;

        IsDownloading = true;
        DownloadStatus = "Starting download…";
        var progress = new Progress<ModelDownloader.Progress>(p =>
        {
            DownloadPercent = p.Percent;
            var mb = p.DownloadedBytes / 1_000_000.0;
            var totalMb = p.TotalBytes / 1_000_000.0;
            var speed = p.BytesPerSecond / 1_000_000.0;
            DownloadStatus = $"Downloading… {p.Percent:F0}% ({mb:F1} / {totalMb:F1} MB, {speed:F1} MB/s)";
        });
        try
        {
            await _host.ModelDownloader.DownloadAsync(model, progress, ct);
            DownloadStatus = $"{model.DisplayName} ready.";
            _log.LogInformation("Language model {ModelId} downloaded for {LangCode}", model.Id, LangCode);
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Error: {ex.Message}";
            _log.LogError(ex, "Error downloading language model {ModelId}", SelectedModelId);
        }
        finally
        {
            IsDownloading = false;
            // Re-evaluate CanDownload so the Download button hides when the file now exists
            OnPropertyChanged(nameof(CanDownload));
        }
    }

    internal Action? CloseRequested;

    [RelayCommand]
    private void Cancel()
    {
        DownloadCommand.Cancel();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void SaveAssignment()
    {
        var modelId = string.IsNullOrEmpty(SelectedModelId) ? null : SelectedModelId;
        _host.UpdateConfig(_host.Config.WithLanguageModel(LangCode, modelId));
        _log.LogInformation("Saved language model assignment: {LangCode} -> {ModelId}", LangCode, modelId ?? "(global)");
        CloseRequested?.Invoke();
    }
}
