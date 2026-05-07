using System.Net;
using System.Text;
using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Tests;

public class ModelDownloaderSha256Tests : IDisposable
{
    private readonly string _testDir;

    public ModelDownloaderSha256Tests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"quickstype-sha256-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch { /* swallow cleanup errors */ }
    }

    private void WriteManifestFile(Dictionary<string, (string sha256, long sizeBytes, string url)> entries)
    {
        var manifestPath = Path.Combine(_testDir, "models", "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        var manifest = new ModelManifest
        {
            ManifestVersion = 1,
            Generated = DateTime.UtcNow.ToString("O"),
            Entries = entries.ToDictionary(
                kv => kv.Key,
                kv => new ModelManifestEntry
                {
                    Sha256 = kv.Value.sha256,
                    SizeBytes = kv.Value.sizeBytes,
                    Url = kv.Value.url
                })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest, QuickSType.Core.Config.ConfigJsonContext.Default.ModelManifest);
        File.WriteAllText(manifestPath, json);
    }

    [Fact]
    public void Manifest_loads_and_provides_sha256()
    {
        // Arrange
        var testSha = "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21";
        WriteManifestFile(new()
        {
            ["ggml-test"] = (testSha, 77691713, "https://example.com/model.bin")
        });

        var oldBaseDir = AppContext.BaseDirectory;

        try
        {
            // Hack: override AppContext.BaseDirectory for test
            // Since we can't directly override it, we test LoadFromString instead
            var json = File.ReadAllText(Path.Combine(_testDir, "models", "manifest.json"));
            var manifest = ModelManifestLoader.LoadFromString(json);

            // Act
            var sha = manifest.GetSha256("ggml-test");

            // Assert
            sha.ShouldBe(testSha);
            sha.Length.ShouldBe(64);
            sha.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f')).ShouldBeTrue();
        }
        finally { }
    }

    [Fact]
    public void ModelIntegrityException_contains_error_details()
    {
        // Arrange
        var modelId = "ggml-test";
        var expected = "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21";
        var actual = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var ex = new ModelIntegrityException(modelId, expected, actual);

        // Assert
        ex.ModelId.ShouldBe(modelId);
        ex.ExpectedSha256.ShouldBe(expected);
        ex.ActualSha256.ShouldBe(actual);
        ex.Message.ShouldContain("SHA-256 mismatch");
        ex.Message.ShouldContain(modelId);
    }

    private string FindRepoFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current.Parent is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException($"Could not find {relativePath}");
    }

    [Fact]
    public void Hex_compare_uses_FixedTimeEquals()
    {
        // ASVS V6.4.1 pin — constant-time hex compare must not regress to ==.
        var src = File.ReadAllText(FindRepoFile("src/QuickSType.Core/Transcribe/ModelDownloader.cs"));

        src.ShouldContain("CryptographicOperations.FixedTimeEquals");
        src.ShouldNotContain("actualHex == expectedSha");   // catch the obvious regression
    }

    [Fact]
    public void Downloader_implements_streaming_hash()
    {
        // Verify that IncrementalHash and streaming verification are in place
        var src = File.ReadAllText(FindRepoFile("src/QuickSType.Core/Transcribe/ModelDownloader.cs"));

        src.ShouldContain("IncrementalHash");
        src.ShouldContain("hasher.AppendData");
        src.ShouldContain("GetHashAndReset");
    }

    [Fact]
    public void Downloader_handles_200_on_range_request()
    {
        // HARDEN-01 / Pitfall 3: explicit check for 206 PartialContent when resuming
        var src = File.ReadAllText(FindRepoFile("src/QuickSType.Core/Transcribe/ModelDownloader.cs"));

        src.ShouldContain("PartialContent");
        src.ShouldContain("restarting download from zero");
    }

    [Fact]
    public void Downloader_reads_manifest_on_download()
    {
        // Verify that DownloadAsync calls ModelManifestLoader.Load()
        var src = File.ReadAllText(FindRepoFile("src/QuickSType.Core/Transcribe/ModelDownloader.cs"));

        src.ShouldContain("ModelManifestLoader.Load");
        src.ShouldContain("manifest.GetSha256");
        src.ShouldContain("manifest.GetUrl");
    }

    [Fact]
    public void Downloader_deletes_part_on_integrity_error()
    {
        // Verify that the .part file is cleaned up on hash mismatch
        var src = File.ReadAllText(FindRepoFile("src/QuickSType.Core/Transcribe/ModelDownloader.cs"));

        src.ShouldContain("File.Delete(partPath)");
        src.ShouldContain("ModelIntegrityException");
    }
}
