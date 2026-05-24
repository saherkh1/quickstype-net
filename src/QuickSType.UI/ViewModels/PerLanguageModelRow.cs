using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;

namespace QuickSType.UI.ViewModels;

public sealed record LanguageModelOption(string Id, string DisplayName)
{
    public static readonly LanguageModelOption UseGlobal = new(string.Empty, "Use general model");
}

public sealed partial class PerLanguageModelRow : ObservableObject
{
    private readonly AppHost _host;
    private bool _syncing;

    public string LangCode { get; }
    public string DisplayName { get; }
    public string HeadingText => $"{DisplayName} model";
    public string SubtitleText => $"Used when input is {DisplayName}.";
    public ObservableCollection<LanguageModelOption> Options { get; }

    [ObservableProperty] private LanguageModelOption _selectedOption;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty] private string _downloadStatus = string.Empty;

    internal Action? DownloadCompleted;

    public PerLanguageModelRow(string langCode, string displayName, AppHost host, AppConfig config)
    {
        LangCode = langCode;
        DisplayName = displayName;
        _host = host;

        var opts = new List<LanguageModelOption> { LanguageModelOption.UseGlobal };
        if (ModelCatalog.LanguageModels.TryGetValue(langCode, out var models))
            opts.AddRange(models.Select(m => new LanguageModelOption(m.Id, m.DisplayName)));
        Options = new ObservableCollection<LanguageModelOption>(opts);

        config.LanguageModels.TryGetValue(langCode, out var currentId);
        _selectedOption = Options.FirstOrDefault(o => o.Id == (currentId ?? string.Empty))
            ?? Options[0];
    }

    internal void SyncFromConfig(AppConfig config)
    {
        _syncing = true;
        try
        {
            config.LanguageModels.TryGetValue(LangCode, out var currentId);
            var opt = Options.FirstOrDefault(o => o.Id == (currentId ?? string.Empty)) ?? Options[0];
            if (opt != SelectedOption) SelectedOption = opt;
        }
        finally { _syncing = false; }
    }

    partial void OnSelectedOptionChanged(LanguageModelOption value)
    {
        if (_syncing) return;
        var modelId = string.IsNullOrEmpty(value.Id) ? null : value.Id;
        _host.UpdateConfig(_host.Config.WithLanguageModel(LangCode, modelId));
        if (!string.IsNullOrEmpty(value.Id) && !ModelCatalog.IsInstalled(value.Id))
            _ = StartDownloadAsync(value.Id);
    }

    private async Task StartDownloadAsync(string modelId)
    {
        var model = ModelCatalog.Find(modelId);
        if (model is null) return;

        IsDownloading = true;
        DownloadPercent = 0;
        DownloadStatus = "Starting download…";

        var progress = new Progress<ModelDownloader.Progress>(p =>
        {
            DownloadPercent = p.Percent;
            var mb = p.DownloadedBytes / 1_000_000.0;
            var totalMb = p.TotalBytes / 1_000_000.0;
            var speed = p.BytesPerSecond / 1_000_000.0;
            DownloadStatus = $"{p.Percent:F0}%  ({mb:F0} / {totalMb:F0} MB  {speed:F1} MB/s)";
        });

        Exception? err = null;
        try
        {
            await _host.ModelDownloader.DownloadAsync(model, progress);
        }
        catch (Exception ex) { err = ex; }

        // Marshal final state back to UI thread — can't await in finally
        Dispatcher.UIThread.Post(() =>
        {
            IsDownloading = false;
            if (err is null)
            {
                DownloadStatus = $"{model.DisplayName} ready.";
                DownloadCompleted?.Invoke();
            }
            else
            {
                DownloadStatus = $"Error: {err.Message}";
            }
        });
    }
}
