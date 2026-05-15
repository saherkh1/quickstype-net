using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Config;

namespace QuickSType.Core.Transcribe;

public static class HallucinationManifestLoader
{
    private static readonly IReadOnlyList<string> SeedPhrases = new[]
    {
        "Thanks for watching",
        "Thanks for watching!",
        "Subtitles by",
        "Subtitles by the Amara.org community",
        "Sous-titres réalisés par la communauté d'Amara.org",
        "Untertitelung aufgrund der Amara.org-Community",
        "Sottotitoli creati dalla comunità Amara.org",
        "Subtítulos realizados por la comunidad de Amara.org",
        "Legendas pela comunidade Amara.org",
        "www.mooji.org",
        "ご視聴ありがとうございました",
    };

    public static HallucinationManifest Load(string modelId, ILogger? log = null)
    {
        log ??= NullLogger.Instance;
        var path = ModelCatalog.DenyListPathFor(modelId);
        if (!File.Exists(path))
        {
            log.LogDebug("No on-disk hallucinations manifest for {Id}; using seed list", modelId);
            return SeedManifest();
        }
        try
        {
            var json = File.ReadAllText(path);
            var parsed = System.Text.Json.JsonSerializer.Deserialize(json, ConfigJsonContext.Default.HallucinationManifest);
            if (parsed is null || parsed.Phrases is null || parsed.Phrases.Count == 0)
            {
                log.LogWarning("Hallucinations manifest for {Id} parsed empty; using seed list", modelId);
                return SeedManifest();
            }
            return parsed;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to load hallucinations manifest for {Id}; using seed list", modelId);
            return SeedManifest();
        }
    }

    private static HallucinationManifest SeedManifest() =>
        new() { Version = 1, Phrases = new List<string>(SeedPhrases) };
}
