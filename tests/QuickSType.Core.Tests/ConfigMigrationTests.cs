using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class ConfigMigrationTests
{
    [Fact]
    public void Maps_python_alt_r_to_VcRightAlt()
    {
        ConfigStore.MapPythonHotkey("alt_r").ShouldBe("VcRightAlt");
    }

    [Theory]
    [InlineData("alt_l", "VcLeftAlt")]
    [InlineData("ctrl_r", "VcRightControl")]
    [InlineData("cmd", "VcLeftMeta")]
    [InlineData("f5", "VcF5")]
    [InlineData("f13", "VcF13")]
    [InlineData(null, "VcRightAlt")]
    [InlineData("", "VcRightAlt")]
    [InlineData("unknown_garbage", "VcRightAlt")]
    public void Hotkey_mappings(string? input, string expected)
    {
        ConfigStore.MapPythonHotkey(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("mlx-community/whisper-tiny", "ggml-tiny")]
    [InlineData("mlx-community/whisper-base", "ggml-base")]
    [InlineData("mlx-community/whisper-small", "ggml-small")]
    [InlineData("mlx-community/whisper-medium", "ggml-medium")]
    [InlineData("mlx-community/whisper-large-v3", "ggml-large-v3")]
    [InlineData("mlx-community/whisper-large-v3-turbo", "ggml-large-v3-turbo-q5_0")]
    [InlineData("ggml-base", "ggml-base")]
    [InlineData(null, "ggml-base")]
    [InlineData("totally-unknown", "ggml-base")]
    public void Model_mappings(string? input, string expected)
    {
        ConfigStore.MapPythonModel(input).ShouldBe(expected);
    }

    [Fact]
    public void Migrates_full_legacy_config()
    {
        var legacy = new LegacyPythonConfig
        {
            Model = "mlx-community/whisper-large-v3-turbo",
            Languages = new() { "en", "he" },
            Hotkey = "alt_r",
            AutoLanguage = true,
        };

        var migrated = ConfigStore.MigrateFromPython(legacy);

        migrated.Model.ShouldBe("ggml-large-v3-turbo-q5_0");
        migrated.Languages.ShouldBe(new[] { "en", "he" });
        migrated.ActiveLanguage.ShouldBe("en");
        migrated.Hotkey.ShouldBe("VcRightAlt");
        migrated.AutoLanguage.ShouldBeTrue();
        migrated.SchemaVersion.ShouldBe(2);
    }

    [Fact]
    public void Migration_defaults_when_empty()
    {
        var migrated = ConfigStore.MigrateFromPython(new LegacyPythonConfig());
        migrated.Model.ShouldBe("ggml-base");
        migrated.Languages.ShouldBe(new[] { "en" });
        migrated.ActiveLanguage.ShouldBe("en");
        migrated.AutoLanguage.ShouldBeFalse();
    }

    [Fact]
    public void DQ3_alt_r_migration_stays_at_VcRightAlt_post_HARDEN_02()
    {
        // D-Q3 (Resolved Decision in 01-RESEARCH.md, post-research triage 2026-05-07):
        // Conservative migration. HARDEN-02 flips the C# DEFAULT to VcRightCtrl, but
        // legacy Python users who upgrade get the FAITHFUL Python intent (alt_r = Right-Alt).
        // German/French Windows users on legacy configs remain AltGr-collided until they
        // rebind via Settings UI — unchanged from pre-upgrade behavior.
        ConfigStore.MapPythonHotkey("alt_r").ShouldBe("VcRightAlt");
        ConfigStore.MapPythonHotkey("alt_r").ShouldNotBe("VcRightCtrl");   // explicit non-flip pin
    }

    [Fact]
    public void Full_legacy_python_config_with_alt_r_migrates_to_VcRightAlt()
    {
        var legacy = new LegacyPythonConfig { Hotkey = "alt_r" };
        var migrated = ConfigStore.MigrateFromPython(legacy);
        migrated.Hotkey.ShouldBe("VcRightAlt");
        // The post-flip AppConfig default would have been "VcRightCtrl", but the migration
        // code path explicitly returns the Python-faithful value.
    }
}
