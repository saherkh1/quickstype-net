using System.IO;
using System.Text;
using System.Text.Json;
using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class ConfigMigrationV3Tests
{
    [Fact]
    public void MigrateV2ToV3_sets_EnableCrashTelemetry_default_false()
    {
        var v2 = new AppConfig { SchemaVersion = 2 };
        var v3 = ConfigStore.MigrateV2ToV3(v2);
        v3.EnableCrashTelemetry.ShouldBeFalse();
    }

    [Fact]
    public void MigrateV2ToV3_sets_KeyboardLayoutDriven_default_false()
    {
        var v2 = new AppConfig { SchemaVersion = 2 };
        var v3 = ConfigStore.MigrateV2ToV3(v2);
        v3.KeyboardLayoutDriven.ShouldBeFalse();
    }

    [Fact]
    public void MigrateV2ToV3_sets_StreamingMode_default_auto()
    {
        var v2 = new AppConfig { SchemaVersion = 2 };
        var v3 = ConfigStore.MigrateV2ToV3(v2);
        v3.StreamingMode.ShouldBe("auto");
    }

    [Fact]
    public void MigrateV2ToV3_sets_PreferredModel_default_null()
    {
        var v2 = new AppConfig { SchemaVersion = 2 };
        var v3 = ConfigStore.MigrateV2ToV3(v2);
        v3.PreferredModel.ShouldBeNull();
    }

    [Fact]
    public void MigrateV2ToV3_bumps_SchemaVersion_to_3()
    {
        var v2 = new AppConfig { SchemaVersion = 2 };
        var v3 = ConfigStore.MigrateV2ToV3(v2);
        v3.SchemaVersion.ShouldBe(3);
    }

    [Fact]
    public void MigrateV2ToV3_preserves_all_existing_v2_fields()
    {
        var v2 = new AppConfig
        {
            Model = "ggml-large-v3-turbo-q5_0",
            Languages = new() { "en", "he" },
            ActiveLanguage = "he",
            Hotkey = "VcF13",
            AutoLanguage = true,
            StartAtLogin = true,
            SelectedAudioDevice = "Built-in Mic",
            TranscriptionBackend = "metal",
            ShowNotifications = false,
            SchemaVersion = 2,
        };
        var v3 = ConfigStore.MigrateV2ToV3(v2);
        v3.Model.ShouldBe("ggml-large-v3-turbo-q5_0");
        v3.Languages.ShouldBe(new[] { "en", "he" });
        v3.ActiveLanguage.ShouldBe("he");
        v3.Hotkey.ShouldBe("VcF13");
        v3.AutoLanguage.ShouldBeTrue();
        v3.StartAtLogin.ShouldBeTrue();
        v3.SelectedAudioDevice.ShouldBe("Built-in Mic");
        v3.TranscriptionBackend.ShouldBe("metal");
        v3.ShowNotifications.ShouldBeFalse();
        v3.SchemaVersion.ShouldBe(3);
    }

    [Fact]
    public void Load_migrates_v2_config_on_disk_to_v3()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            // Write a v2 config to disk (only fields present in v2)
            var v2Json = "{\"model\":\"ggml-small\",\"languages\":[\"en\"],\"active_language\":\"en\",\"hotkey\":\"VcRightCtrl\",\"auto_language\":false,\"start_at_login\":false,\"selected_audio_device\":null,\"transcription_backend\":\"auto\",\"show_notifications\":true,\"schema_version\":2}";
            File.WriteAllText(tmp, v2Json, Encoding.UTF8);

            var store = new ConfigStore(overridePath: tmp);
            var loaded = store.Load();

            loaded.SchemaVersion.ShouldBe(3);
            loaded.Model.ShouldBe("ggml-small");
            loaded.PreferredModel.ShouldBeNull();
            loaded.StreamingMode.ShouldBe("auto");

            // Verify file on disk was rewritten with schema_version=3
            var rewritten = File.ReadAllText(tmp);
            rewritten.ShouldContain("\"schema_version\": 3");
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
