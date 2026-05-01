using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests;

public class ModelCatalogTests
{
    [Fact]
    public void Default_is_turbo_quant()
    {
        ModelCatalog.Default.Id.ShouldBe("ggml-large-v3-turbo-q5_0");
    }

    [Theory]
    [InlineData("ggml-tiny")]
    [InlineData("ggml-base")]
    [InlineData("ggml-small")]
    [InlineData("ggml-medium")]
    [InlineData("ggml-large-v3")]
    [InlineData("ggml-large-v3-turbo")]
    [InlineData("ggml-large-v3-turbo-q5_0")]
    public void Catalog_contains_known_ids(string id)
    {
        ModelCatalog.Find(id).ShouldNotBeNull();
    }

    [Fact]
    public void Unknown_id_returns_null()
    {
        ModelCatalog.Find("not-a-real-model").ShouldBeNull();
    }

    [Fact]
    public void All_urls_point_at_huggingface()
    {
        foreach (var m in ModelCatalog.All)
        {
            m.Url.ShouldStartWith("https://huggingface.co/");
            m.Url.ShouldEndWith(".bin");
            m.ApproxSizeBytes.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void Path_for_uses_models_dir()
    {
        var p = ModelCatalog.PathFor("ggml-base");
        p.ShouldEndWith("ggml-base.bin");
        p.ShouldContain("models");
    }
}
