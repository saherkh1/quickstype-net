using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickSType.Core.Transcribe;

namespace QuickSType.UI.ViewModels;

public sealed partial class DownloadedModelRow : ObservableObject
{
    private readonly Action<string> _onDeleted;

    public ModelInfo Model { get; }
    public string SizeDisplay { get; }

    public DownloadedModelRow(ModelInfo model, Action<string> onDeleted)
    {
        Model = model;
        _onDeleted = onDeleted;
        SizeDisplay = FormatSize(model.ApproxSizeBytes);
    }

    [RelayCommand]
    private void Delete()
    {
        try { File.Delete(ModelCatalog.PathFor(Model.Id)); }
        catch { /* best-effort */ }
        _onDeleted(Model.Id);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1} GB";
        return $"{bytes / 1_000_000:F0} MB";
    }
}
