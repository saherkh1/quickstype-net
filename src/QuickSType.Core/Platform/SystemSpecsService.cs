using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Transcribe;
using Whisper.net;
using Whisper.net.LibraryLoader;
using IoPath = System.IO.Path;

namespace QuickSType.Core.Platform;

/// <summary>
/// BCL-only implementation of <see cref="ISystemSpecsService"/>. No platform branch needed:
/// every probe (RAM, disk, architecture, CUDA) is cross-platform.
/// CUDA probe uses Whisper.net's own <see cref="RuntimeOptions"/> save/restore pattern (Pattern 4)
/// and is skipped entirely on non-Windows.
/// </summary>
public sealed class SystemSpecsService : ISystemSpecsService
{
    /// <summary>Single source of truth — minimum RAM (GiB) to run the smallest Whisper model.</summary>
    public const double MinRamGb = 2.0;
    /// <summary>Single source of truth — minimum free disk (GiB) to run the smallest Whisper model.</summary>
    public const double MinFreeDiskGb = 1.0;

    double ISystemSpecsService.MinRamGb => MinRamGb;
    double ISystemSpecsService.MinFreeDiskGb => MinFreeDiskGb;

    private readonly ILogger _log;
    private SystemSpecs? _cached;

    public SystemSpecsService(ILogger<SystemSpecsService>? log = null)
        => _log = (ILogger?)log ?? NullLogger.Instance;

    public SystemSpecs Detect()
    {
        if (_cached is not null) return _cached;

        // Pitfall 1: TotalAvailableMemoryBytes is the OS-reported physical RAM, not the managed heap.
        var totalRamBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var ramGb = totalRamBytes / 1024.0 / 1024.0 / 1024.0;

        double freeGb;
        try
        {
            var modelsDir = ModelCatalog.ModelsDirectory();
            var driveRoot = IoPath.GetPathRoot(modelsDir) ?? "/";
            freeGb = new DriveInfo(driveRoot).AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to query free disk; defaulting to MaxValue (will not block)");
            freeGb = double.MaxValue;
        }

        var isAppleSilicon =
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        var cudaAvailable = !isAppleSilicon
            && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && DetectCuda();

        var tier = ClassifyTier(isAppleSilicon, cudaAvailable, ramGb, freeGb);

        _log.LogInformation(
            "Detected hardware: {Tier} ({RamGb:F1} GB RAM, {FreeDiskGb:F1} GB free)",
            tier, ramGb, freeGb);

        _cached = new SystemSpecs(ramGb, freeGb, cudaAvailable, tier);
        return _cached;
    }

    public bool IsStreamingCapable()
    {
        var specs = Detect();
        return specs.Tier is HardwareTier.AppleSilicon or HardwareTier.WindowsCuda or HardwareTier.Ram16Plus;
    }

    public ModelInfo RecommendModel(SystemSpecs specs) => specs.Tier switch
    {
        HardwareTier.AppleSilicon => ModelCatalog.Find("ggml-small") ?? ModelCatalog.Default,
        HardwareTier.WindowsCuda  => ModelCatalog.Find("ggml-small") ?? ModelCatalog.Default,
        HardwareTier.Ram16Plus    => ModelCatalog.Find("ggml-small") ?? ModelCatalog.Default,
        HardwareTier.Ram8To16     => ModelCatalog.Find("ggml-base")  ?? ModelCatalog.Default,
        _                          => ModelCatalog.Find("ggml-tiny") ?? ModelCatalog.Default,
    };

    public bool IsBlocker(SystemSpecs specs)
        => specs.TotalRamGb < MinRamGb || specs.FreeDiskGb < MinFreeDiskGb;

    public string? GetHardwareWarning(ModelInfo model, SystemSpecs specs)
    {
        // RAM precedence: if both apply, the RAM message wins.
        if (model.ApproxSizeBytes > specs.TotalRamGb * 1024.0 * 1024.0 * 1024.0)
            return $"⚠ May exceed your system RAM ({specs.TotalRamGb:F0} GB detected)";

        var modelMb = (int)Math.Round(model.ApproxSizeBytes / 1_000_000.0);
        if (model.ApproxSizeBytes > specs.FreeDiskGb * 1024.0 * 1024.0 * 1024.0)
            return $"⚠ Not enough free disk ({specs.FreeDiskGb:F1} GB free, model needs {modelMb} MB)";

        return null;
    }

    private static HardwareTier ClassifyTier(bool isAppleSilicon, bool cuda, double ramGb, double freeGb)
    {
        if (isAppleSilicon) return HardwareTier.AppleSilicon;
        if (cuda)            return HardwareTier.WindowsCuda;
        if (ramGb >= 16.0)   return HardwareTier.Ram16Plus;
        if (ramGb >= 8.0)    return HardwareTier.Ram8To16;
        return HardwareTier.LowResource;
    }

    /// <summary>
    /// Pattern 4: probe Whisper.net CUDA availability without loading a model.
    /// CRITICAL: <see cref="RuntimeOptions.RuntimeLibraryOrder"/> is a static List —
    /// must save/restore around the probe (Pitfall 2).
    /// MUST be called BEFORE any <see cref="WhisperFactory"/> is constructed elsewhere.
    /// </summary>
    private static bool DetectCuda()
    {
        var saved = RuntimeOptions.RuntimeLibraryOrder.ToList();
        try
        {
            RuntimeOptions.RuntimeLibraryOrder.Clear();
            RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.Cuda);
            WhisperFactory.GetRuntimeInfo();
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            // Any other failure (DLL load, bad path) — treat as no CUDA, don't crash startup.
            return false;
        }
        finally
        {
            RuntimeOptions.RuntimeLibraryOrder.Clear();
            RuntimeOptions.RuntimeLibraryOrder.AddRange(saved);
        }
    }
}
