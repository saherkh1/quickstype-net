using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Platform;

/// <summary>
/// Hardware-tier classification for first-run model picker (CONFIG-02) and
/// blocker-dialog gate (CONFIG-03).
/// </summary>
public enum HardwareTier
{
    /// <summary>macOS on Apple Silicon (M-series). Always recommended ggml-small.</summary>
    AppleSilicon,
    /// <summary>Windows with CUDA runtime detected via Whisper.net probe. Recommended ggml-small.</summary>
    WindowsCuda,
    /// <summary>Win/Intel-Mac, >=16 GB RAM, no CUDA. Recommended ggml-small.</summary>
    Ram16Plus,
    /// <summary>Win/Intel-Mac, 8-16 GB RAM, no CUDA. Recommended ggml-base.</summary>
    Ram8To16,
    /// <summary>Win/Intel-Mac, &lt;8 GB RAM, no CUDA. Recommended ggml-tiny. Below 2 GB RAM = blocker.</summary>
    LowResource,
}

/// <summary>
/// Snapshot of the host machine's hardware characteristics. Constructed once per process
/// (per D-08, called from <see cref="QuickSType.UI.Composition.AppHost"/>) and passed
/// downstream to the blocker-dialog gate, the first-run banner, and per-model warnings.
/// </summary>
/// <param name="TotalRamGb">Physical RAM available to this process, in GiB.</param>
/// <param name="FreeDiskGb">Free space on the drive containing the models directory, in GiB.</param>
/// <param name="CudaAvailable">True iff Whisper.net's CUDA runtime probe succeeded (Windows only).</param>
/// <param name="Tier">Classification used by <see cref="ISystemSpecsService.RecommendModel"/>.</param>
public sealed record SystemSpecs(double TotalRamGb, double FreeDiskGb, bool CudaAvailable, HardwareTier Tier);

/// <summary>
/// Port for hardware-spec detection and model-fit advice. One implementation
/// (<see cref="SystemSpecsService"/>) — BCL-only, no platform branch needed
/// because every probe (RAM, disk, CUDA, architecture) is cross-platform.
/// </summary>
public interface ISystemSpecsService
{
    /// <summary>Probe the host once and return a snapshot. Call exactly once at startup (Pitfall: see RuntimeOptions caveat).</summary>
    SystemSpecs Detect();

    /// <summary>Recommended Whisper model for the given specs. Always returns a non-null member of <see cref="ModelCatalog.All"/>.</summary>
    ModelInfo RecommendModel(SystemSpecs specs);

    /// <summary>Warning string for a model under given specs (UI-SPEC S3). RAM warning takes precedence over disk warning. Null when the model fits.</summary>
    string? GetHardwareWarning(ModelInfo model, SystemSpecs specs);

    /// <summary>True iff the host cannot run even the smallest model (RAM &lt; 2 GB or free disk &lt; 1 GB). Drives the pre-tray blocker dialog.</summary>
    bool IsBlocker(SystemSpecs specs);
}
