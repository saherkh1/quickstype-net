using CommunityToolkit.Mvvm.ComponentModel;
using QuickSType.Core.Platform;
using QuickSType.Core.Transcribe;

namespace QuickSType.UI.ViewModels;

public sealed partial class ModelRowViewModel : ObservableObject
{
    public ModelInfo Model { get; }
    public string? HardwareWarning { get; }

    public bool IsInstalled => ModelCatalog.IsInstalled(Model.Id);
    public bool IsLanguageSpecific => Model.LanguageCode is not null;
    public string LanguageTag => Model.LanguageCode?.ToUpperInvariant() ?? string.Empty;

    public ModelRowViewModel(ModelInfo model, SystemSpecs specs, ISystemSpecsService svc)
    {
        Model = model;
        HardwareWarning = svc.GetHardwareWarning(model, specs);
    }

    public void RefreshInstallStatus() => OnPropertyChanged(nameof(IsInstalled));
}
