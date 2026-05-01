using System.Text.Json;
using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class ConfigRoundTripTests
{
    [Fact]
    public void All_fields_round_trip_through_source_gen()
    {
        var original = new AppConfig
        {
            Model = "ggml-large-v3-turbo-q5_0",
            Languages = new() { "en", "he", "ar" },
            ActiveLanguage = "he",
            Hotkey = "VcRightAlt",
            AutoLanguage = true,
            StartAtLogin = true,
            SelectedAudioDevice = "MacBook Pro Microphone",
            TranscriptionBackend = "auto",
            ShowNotifications = false,
            SchemaVersion = 2,
        };

        var json = JsonSerializer.Serialize(original, ConfigJsonContext.Default.AppConfig);
        var rt = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);

        rt.ShouldNotBeNull();
        rt!.Model.ShouldBe(original.Model);
        rt.Languages.ShouldBe(original.Languages);
        rt.ActiveLanguage.ShouldBe(original.ActiveLanguage);
        rt.Hotkey.ShouldBe(original.Hotkey);
        rt.AutoLanguage.ShouldBe(original.AutoLanguage);
        rt.StartAtLogin.ShouldBe(original.StartAtLogin);
        rt.SelectedAudioDevice.ShouldBe(original.SelectedAudioDevice);
        rt.TranscriptionBackend.ShouldBe(original.TranscriptionBackend);
        rt.ShowNotifications.ShouldBe(original.ShowNotifications);
        rt.SchemaVersion.ShouldBe(original.SchemaVersion);
    }

    [Fact]
    public void With_language_adds_and_activates()
    {
        var c = new AppConfig { Languages = new() { "en" }, ActiveLanguage = "en" };
        var c2 = c.WithLanguage("he");
        c2.ActiveLanguage.ShouldBe("he");
        c2.Languages.ShouldContain("he");
        c2.AutoLanguage.ShouldBeFalse();
    }

    [Fact]
    public void With_auto_language_flips_flag()
    {
        var c = new AppConfig { AutoLanguage = false };
        c.WithAutoLanguage().AutoLanguage.ShouldBeTrue();
    }
}
