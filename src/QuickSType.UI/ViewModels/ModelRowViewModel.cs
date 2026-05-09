using QuickSType.Core.Platform;
using QuickSType.Core.Transcribe;

namespace QuickSType.UI.ViewModels;

/// <summary>
/// Wrapper VM that pairs a <see cref="ModelInfo"/> with a precomputed hardware-fit warning
/// for the Models-tab ComboBox (UI-SPEC S3). HardwareWarning is null when the model fits.
/// </summary>
public sealed class ModelRowViewModel
{
    public ModelInfo Model { get; }
    public string? HardwareWarning { get; }

    public ModelRowViewModel(ModelInfo model, SystemSpecs specs, ISystemSpecsService svc)
    {
        Model = model;
        HardwareWarning = svc.GetHardwareWarning(model, specs);
    }
}
