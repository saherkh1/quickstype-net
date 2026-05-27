using System.Text.Json;
using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class ConfigRoundTripV3Tests
{
    [Fact]
    public void All_v3_fields_round_trip_through_source_gen()
    {
        var original = new AppConfig
        {
            EnableCrashTelemetry = true,
            KeyboardLayoutDriven = true,
            StreamingMode = "cpu",
            EnableStreamingInsertion = false,
            PreferredModel = "ggml-small",
            UpdateChannel = "stable",
            UpdateSourceUrl = "https://github.com/saherk/quickstype",
            ManagedPackageManager = "homebrew",
            EnableBackgroundUpdateChecks = false,
            SchemaVersion = 3,
        };
        var json = JsonSerializer.Serialize(original, ConfigJsonContext.Default.AppConfig);
        var rt = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
        rt.ShouldNotBeNull();
        rt!.EnableCrashTelemetry.ShouldBeTrue();
        rt.KeyboardLayoutDriven.ShouldBeTrue();
        rt.StreamingMode.ShouldBe("cpu");
        rt.EnableStreamingInsertion.ShouldBeFalse();
        rt.PreferredModel.ShouldBe("ggml-small");
        rt.UpdateChannel.ShouldBe("stable");
        rt.UpdateSourceUrl.ShouldBe("https://github.com/saherk/quickstype");
        rt.ManagedPackageManager.ShouldBe("homebrew");
        rt.EnableBackgroundUpdateChecks.ShouldBeFalse();
        rt.SchemaVersion.ShouldBe(3);
    }

    [Fact]
    public void Json_property_names_are_snake_case()
    {
        var cfg = new AppConfig
        {
            EnableCrashTelemetry = true,
            KeyboardLayoutDriven = true,
            StreamingMode = "cpu",
            EnableStreamingInsertion = false,
            PreferredModel = "ggml-small",
            UpdateChannel = "stable",
            UpdateSourceUrl = "https://github.com/saherk/quickstype",
            ManagedPackageManager = "winget",
            EnableBackgroundUpdateChecks = false,
        };
        var json = JsonSerializer.Serialize(cfg, ConfigJsonContext.Default.AppConfig);
        json.ShouldContain("\"enable_crash_telemetry\"");
        json.ShouldContain("\"keyboard_layout_driven\"");
        json.ShouldContain("\"streaming_mode\"");
        json.ShouldContain("\"enable_streaming_insertion\"");
        json.ShouldContain("\"preferred_model\"");
        json.ShouldContain("\"update_channel\"");
        json.ShouldContain("\"update_source_url\"");
        json.ShouldContain("\"managed_package_manager\"");
        json.ShouldContain("\"enable_background_update_checks\"");
    }

    [Fact]
    public void WithPreferredModel_sets_PreferredModel_via_with_expression()
    {
        var original = new AppConfig();
        original.PreferredModel.ShouldBeNull();
        var updated = original.WithPreferredModel("ggml-small");
        updated.PreferredModel.ShouldBe("ggml-small");
        // Record semantics: original is unchanged
        original.PreferredModel.ShouldBeNull();
    }

    [Fact]
    public void WithPreferredModel_null_clears_PreferredModel()
    {
        var cfg = new AppConfig().WithPreferredModel("ggml-small");
        var cleared = cfg.WithPreferredModel(null);
        cleared.PreferredModel.ShouldBeNull();
    }

    [Fact]
    public void New_AppConfig_default_SchemaVersion_is_4()
    {
        // SchemaVersion default is 4 after Phase 8 — new installs skip migration on second launch
        new AppConfig().SchemaVersion.ShouldBe(4);
    }

    [Fact]
    public void Default_StreamingMode_is_auto()
    {
        new AppConfig().StreamingMode.ShouldBe("auto");
    }

    [Fact]
    public void EnableStreamingInsertion_defaults_to_true()
    {
        new AppConfig().EnableStreamingInsertion.ShouldBeTrue();
    }

    [Fact]
    public void Default_PreferredModel_is_null()
    {
        new AppConfig().PreferredModel.ShouldBeNull();
    }

    [Fact]
    public void EnableVibrancy_defaults_to_null()
    {
        new AppConfig().EnableVibrancy.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableVibrancy_round_trips_through_source_gen(bool? value)
    {
        var cfg = new AppConfig { EnableVibrancy = value };
        var json = System.Text.Json.JsonSerializer.Serialize(cfg, ConfigJsonContext.Default.AppConfig);
        var rt = System.Text.Json.JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
        rt!.EnableVibrancy.ShouldBe(value);
    }

    [Fact]
    public void Json_property_name_for_enable_vibrancy_is_snake_case()
    {
        var cfg = new AppConfig { EnableVibrancy = true };
        var json = System.Text.Json.JsonSerializer.Serialize(cfg, ConfigJsonContext.Default.AppConfig);
        json.ShouldContain("\"enable_vibrancy\"");
    }

    [Fact]
    public void Update_defaults_are_canary_unmanaged_and_background_checks_enabled()
    {
        var cfg = new AppConfig();
        cfg.UpdateChannel.ShouldBe("canary");
        cfg.UpdateSourceUrl.ShouldBeNull();
        cfg.ManagedPackageManager.ShouldBeNull();
        cfg.EnableBackgroundUpdateChecks.ShouldBeTrue();
    }

    [Fact]
    public void Update_fields_round_trip_through_source_gen()
    {
        var cfg = new AppConfig
        {
            UpdateChannel = "stable",
            UpdateSourceUrl = "file:///tmp/quickstype-updates",
            ManagedPackageManager = "winget",
            EnableBackgroundUpdateChecks = false,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(cfg, ConfigJsonContext.Default.AppConfig);
        var rt = System.Text.Json.JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);

        rt!.UpdateChannel.ShouldBe("stable");
        rt.UpdateSourceUrl.ShouldBe("file:///tmp/quickstype-updates");
        rt.ManagedPackageManager.ShouldBe("winget");
        rt.EnableBackgroundUpdateChecks.ShouldBeFalse();
    }
}
