using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests;

public class ModelManifestTests
{
    [Fact]
    public void Manifest_deserializes_via_source_gen()
    {
        var manifest = ModelManifestLoader.Load();

        manifest.ManifestVersion.ShouldBe(1);
        manifest.Generated.ShouldNotBeNull();
        manifest.Entries.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetSha256_returns_64_char_hex_string()
    {
        var manifest = ModelManifestLoader.Load();
        var sha = manifest.GetSha256("ggml-tiny");

        sha.Length.ShouldBe(64);
        sha.ShouldBeOfType<string>();
        // All characters should be hex digits (0-9, a-f)
        sha.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f')).ShouldBeTrue();
    }

    [Fact]
    public void GetSha256_throws_on_missing_model()
    {
        var manifest = ModelManifestLoader.Load();

        var ex = Should.Throw<ArgumentException>(() => manifest.GetSha256("nonexistent-model"));
        ex.Message.ShouldContain("nonexistent-model");
        ex.Message.ShouldContain("not present in manifest");
    }

    [Fact]
    public void Every_catalog_model_has_a_manifest_entry()
    {
        var manifest = ModelManifestLoader.Load();

        foreach (var info in ModelCatalog.All)
        {
            manifest.Entries.ShouldContainKey(info.Id);
        }
    }
}
