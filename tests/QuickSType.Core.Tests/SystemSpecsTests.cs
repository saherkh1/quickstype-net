using QuickSType.Core.Platform;
using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests;

public class SystemSpecsTests
{
    private static SystemSpecsService NewService() => new();

    [Theory]
    [InlineData(HardwareTier.AppleSilicon, "ggml-small")]
    [InlineData(HardwareTier.WindowsCuda,  "ggml-small")]
    [InlineData(HardwareTier.Ram16Plus,    "ggml-small")]
    [InlineData(HardwareTier.Ram8To16,     "ggml-base")]
    [InlineData(HardwareTier.LowResource,  "ggml-tiny")]
    public void RecommendModel_returns_correct_id_for_each_tier(HardwareTier tier, string expectedId)
    {
        var svc = NewService();
        var specs = new SystemSpecs(TotalRamGb: 16.0, FreeDiskGb: 100.0, CudaAvailable: false, Tier: tier);
        svc.RecommendModel(specs).Id.ShouldBe(expectedId);
    }

    [Fact]
    public void IsBlocker_true_when_ram_below_2GB()
    {
        var svc = NewService();
        svc.IsBlocker(new SystemSpecs(1.5, 10.0, false, HardwareTier.LowResource)).ShouldBeTrue();
    }

    [Fact]
    public void IsBlocker_true_when_disk_below_1GB()
    {
        var svc = NewService();
        svc.IsBlocker(new SystemSpecs(8.0, 0.5, false, HardwareTier.Ram8To16)).ShouldBeTrue();
    }

    [Fact]
    public void IsBlocker_false_for_2GB_RAM_and_1GB_disk_boundary()
    {
        var svc = NewService();
        svc.IsBlocker(new SystemSpecs(2.0, 1.0, false, HardwareTier.LowResource)).ShouldBeFalse();
    }

    [Fact]
    public void IsBlocker_false_for_typical_modern_machine()
    {
        var svc = NewService();
        svc.IsBlocker(new SystemSpecs(16.0, 100.0, false, HardwareTier.Ram16Plus)).ShouldBeFalse();
    }

    [Fact]
    public void GetHardwareWarning_returns_RAM_warning_when_model_exceeds_RAM()
    {
        var svc = NewService();
        var hugeModel = ModelCatalog.Find("ggml-large-v3") ?? throw new System.Exception("model missing");
        var specs = new SystemSpecs(TotalRamGb: 0.5, FreeDiskGb: 100.0, CudaAvailable: false, Tier: HardwareTier.LowResource);
        var w = svc.GetHardwareWarning(hugeModel, specs);
        w.ShouldNotBeNull();
        w.ShouldContain("⚠ May exceed your system RAM");
    }

    [Fact]
    public void GetHardwareWarning_returns_disk_warning_when_model_exceeds_disk()
    {
        var svc = NewService();
        var bigModel = ModelCatalog.Find("ggml-small") ?? throw new System.Exception("model missing");
        var specs = new SystemSpecs(TotalRamGb: 16.0, FreeDiskGb: 0.1, CudaAvailable: false, Tier: HardwareTier.Ram16Plus);
        var w = svc.GetHardwareWarning(bigModel, specs);
        w.ShouldNotBeNull();
        w.ShouldContain("⚠ Not enough free disk");
    }

    [Fact]
    public void GetHardwareWarning_returns_null_when_model_fits()
    {
        var svc = NewService();
        var tiny = ModelCatalog.Find("ggml-tiny") ?? throw new System.Exception("model missing");
        var specs = new SystemSpecs(TotalRamGb: 16.0, FreeDiskGb: 100.0, CudaAvailable: false, Tier: HardwareTier.Ram16Plus);
        svc.GetHardwareWarning(tiny, specs).ShouldBeNull();
    }

    [Fact]
    public void GetHardwareWarning_RAM_takes_precedence_when_both_apply()
    {
        var svc = NewService();
        var huge = ModelCatalog.Find("ggml-large-v3") ?? throw new System.Exception("model missing");
        var specs = new SystemSpecs(TotalRamGb: 0.5, FreeDiskGb: 0.5, CudaAvailable: false, Tier: HardwareTier.LowResource);
        var w = svc.GetHardwareWarning(huge, specs);
        w.ShouldNotBeNull();
        w.ShouldStartWith("⚠ May exceed your system RAM");
    }

    [Fact]
    public void Detect_returns_non_null_specs_with_finite_values()
    {
        var svc = NewService();
        var specs = svc.Detect();
        specs.ShouldNotBeNull();
        specs.TotalRamGb.ShouldBeGreaterThan(0.0);
        specs.FreeDiskGb.ShouldBeGreaterThan(0.0);
    }
}
