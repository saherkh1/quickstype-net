using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests;

public class ModelCatalogLanguageTests
{
    [Fact]
    public void LanguageModels_he_contains_ggml_medium_he()
    {
        ModelCatalog.LanguageModels.ShouldContainKey("he");
        ModelCatalog.LanguageModels["he"].ShouldContain(m => m.Id == "ggml-medium-he");
    }

    [Fact]
    public void Arabic_not_in_LanguageModels()
    {
        ModelCatalog.LanguageModels.ContainsKey("ar").ShouldBeFalse();
    }

    [Fact]
    public void Find_ggml_medium_he_returns_non_null()
    {
        ModelCatalog.Find("ggml-medium-he").ShouldNotBeNull();
    }

    [Fact]
    public void ggml_medium_he_has_he_language_code()
    {
        ModelCatalog.Find("ggml-medium-he")!.LanguageCode.ShouldBe("he");
    }

    [Fact]
    public void Global_models_have_null_language_code()
    {
        // ggml-tiny is the canonical first global entry
        ModelCatalog.Find("ggml-tiny")!.LanguageCode.ShouldBeNull();
        // All pre-existing entries should have null LanguageCode
        ModelCatalog.All.Where(m => m.Id != "ggml-medium-he")
                        .All(m => m.LanguageCode is null)
                        .ShouldBeTrue();
    }

    [Fact]
    public void LanguageModels_dictionary_is_case_insensitive()
    {
        ModelCatalog.LanguageModels.ContainsKey("HE").ShouldBeTrue();
        ModelCatalog.LanguageModels["HE"].ShouldContain(m => m.Id == "ggml-medium-he");
    }
}
