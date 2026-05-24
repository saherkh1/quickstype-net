using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class ConfigMigrationV4Tests
{
    [Fact]
    public void MigrateV3ToV4_bumps_schema_version_to_4()
    {
        var v3 = new AppConfig { SchemaVersion = 3 };
        var v4 = ConfigStore.MigrateV3ToV4(v3);

        v4.SchemaVersion.ShouldBe(4);
    }

    [Fact]
    public void MigrateV3ToV4_injects_empty_language_models()
    {
        var v3 = new AppConfig { SchemaVersion = 3 };
        var v4 = ConfigStore.MigrateV3ToV4(v3);

        v4.LanguageModels.ShouldNotBeNull();
        v4.LanguageModels.Count.ShouldBe(0);
    }

    [Fact]
    public void MigrateV3ToV4_preserves_all_v3_fields()
    {
        var v3 = new AppConfig
        {
            Model = "ggml-large-v3-turbo-q5_0",
            Languages = new List<string> { "en", "he" },
            ActiveLanguage = "he",
            Hotkey = "VcRightCtrl",
            AutoLanguage = true,
            StartAtLogin = true,
            SelectedAudioDevice = "Built-in Microphone",
            TranscriptionBackend = "cpu",
            ShowNotifications = false,
            EnableCrashTelemetry = true,
            KeyboardLayoutDriven = true,
            StreamingMode = "commit-on-pause",
            PreferredModel = "ggml-medium",
            SchemaVersion = 3,
        };

        var v4 = ConfigStore.MigrateV3ToV4(v3);

        v4.Model.ShouldBe(v3.Model);
        v4.Languages.ShouldBe(v3.Languages);
        v4.ActiveLanguage.ShouldBe(v3.ActiveLanguage);
        v4.Hotkey.ShouldBe(v3.Hotkey);
        v4.AutoLanguage.ShouldBe(v3.AutoLanguage);
        v4.StartAtLogin.ShouldBe(v3.StartAtLogin);
        v4.SelectedAudioDevice.ShouldBe(v3.SelectedAudioDevice);
        v4.TranscriptionBackend.ShouldBe(v3.TranscriptionBackend);
        v4.ShowNotifications.ShouldBe(v3.ShowNotifications);
        v4.EnableCrashTelemetry.ShouldBe(v3.EnableCrashTelemetry);
        v4.KeyboardLayoutDriven.ShouldBe(v3.KeyboardLayoutDriven);
        v4.StreamingMode.ShouldBe(v3.StreamingMode);
        v4.PreferredModel.ShouldBe(v3.PreferredModel);
    }

    [Fact]
    public void MigrateV3ToV4_does_not_mutate_input()
    {
        var v3 = new AppConfig { SchemaVersion = 3 };
        _ = ConfigStore.MigrateV3ToV4(v3);

        // Input instance is unchanged (records are immutable; with-expressions produce new instances)
        v3.SchemaVersion.ShouldBe(3);
    }
}
