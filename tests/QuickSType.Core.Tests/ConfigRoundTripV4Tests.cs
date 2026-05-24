using System.Text.Json;
using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class ConfigRoundTripV4Tests
{
    [Fact]
    public void LanguageModels_defaults_to_empty_dict()
    {
        new AppConfig().LanguageModels.Count.ShouldBe(0);
    }

    [Fact]
    public void LanguageModels_round_trips_through_source_gen()
    {
        var cfg = new AppConfig
        {
            LanguageModels = new Dictionary<string, string> { ["he"] = "ggml-medium-he", ["ar"] = "ggml-base" },
        };
        var json = JsonSerializer.Serialize(cfg, ConfigJsonContext.Default.AppConfig);
        var rt = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);

        rt.ShouldNotBeNull();
        rt!.LanguageModels.Count.ShouldBe(2);
        rt.LanguageModels["he"].ShouldBe("ggml-medium-he");
        rt.LanguageModels["ar"].ShouldBe("ggml-base");
    }

    [Fact]
    public void LanguageModels_full_round_trip_preserves_empty_map()
    {
        // When the full AppConfig (with language_models key present) is round-tripped,
        // an empty dictionary is preserved — the non-null guarantee holds on the full-json path.
        // Sparse JSON (missing "language_models") is not a supported deserialization path;
        // ConfigStore.Load() always migrates configs to v4 which explicitly sets LanguageModels.
        var cfg = new AppConfig { LanguageModels = new Dictionary<string, string>() };
        var json = JsonSerializer.Serialize(cfg, ConfigJsonContext.Default.AppConfig);
        var rt = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);

        rt.ShouldNotBeNull();
        rt!.LanguageModels.ShouldNotBeNull();
        rt.LanguageModels.Count.ShouldBe(0);
    }

    [Fact]
    public void WithLanguageModel_sets_entry()
    {
        var cfg = new AppConfig();
        var result = cfg.WithLanguageModel("he", "ggml-medium-he");

        result.LanguageModels["he"].ShouldBe("ggml-medium-he");
    }

    [Fact]
    public void WithLanguageModel_replaces_entry()
    {
        var cfg = new AppConfig().WithLanguageModel("he", "ggml-base");
        var result = cfg.WithLanguageModel("he", "ggml-medium-he");

        result.LanguageModels["he"].ShouldBe("ggml-medium-he");
        result.LanguageModels.Count.ShouldBe(1);
    }

    [Fact]
    public void WithLanguageModel_null_removes_entry()
    {
        var cfg = new AppConfig().WithLanguageModel("he", "ggml-medium-he");
        var result = cfg.WithLanguageModel("he", null);

        result.LanguageModels.ContainsKey("he").ShouldBeFalse();
    }

    [Fact]
    public void WithLanguageModel_is_case_insensitive()
    {
        // Set with uppercase, remove with lowercase — should still remove
        var cfg = new AppConfig().WithLanguageModel("HE", "ggml-medium-he");
        cfg.LanguageModels.ContainsKey("HE").ShouldBeTrue();

        var result = cfg.WithLanguageModel("he", null);
        result.LanguageModels.ContainsKey("he").ShouldBeFalse();
        result.LanguageModels.ContainsKey("HE").ShouldBeFalse();
    }
}
