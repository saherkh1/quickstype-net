using System.Text.Json;
using System.Text.Json.Serialization;
using QuickSType.Core.Config;

namespace QuickSType.Core.Transcribe;

public sealed record ModelManifest
{
    [JsonPropertyName("manifest_version")]
    public int ManifestVersion { get; init; } = 1;

    [JsonPropertyName("generated")]
    public string? Generated { get; init; }

    [JsonPropertyName("entries")]
    public Dictionary<string, ModelManifestEntry> Entries { get; init; } = new();

    public string GetSha256(string modelId)
    {
        if (!Entries.TryGetValue(modelId, out var entry))
            throw new ArgumentException($"Model id '{modelId}' not present in manifest", nameof(modelId));
        return entry.Sha256;
    }

    public string GetUrl(string modelId)
    {
        if (!Entries.TryGetValue(modelId, out var entry))
            throw new ArgumentException($"Model id '{modelId}' not present in manifest", nameof(modelId));
        return entry.Url;
    }
}

public sealed record ModelManifestEntry
{
    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

public static class ModelManifestLoader
{
    private const string ManifestRelativePath = "models/manifest.json";

    public static ModelManifest Load()
    {
        var path = ResolveManifestPath();
        var json = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.ModelManifest)
            ?? throw new InvalidOperationException($"Manifest at {path} deserialized to null");
        return manifest;
    }

    public static ModelManifest LoadFromString(string json) =>
        JsonSerializer.Deserialize(json, ConfigJsonContext.Default.ModelManifest)
            ?? throw new InvalidOperationException("Manifest JSON deserialized to null");

    // Resolves the manifest path relative to the running binary OR repo root (test-friendly).
    // In published binaries the manifest is copied next to the executable via <Content Include="models/manifest.json" CopyToOutputDirectory="PreserveNewest" />
    // (added to QuickSType.UI.csproj in 01-05-PLAN.md). For dotnet test, AppContext.BaseDirectory walks up to find the file.
    private static string ResolveManifestPath()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, ManifestRelativePath);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException($"Could not locate {ManifestRelativePath} from {AppContext.BaseDirectory}");
    }
}
