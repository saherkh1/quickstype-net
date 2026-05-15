namespace QuickSType.Core.Transcribe;

public sealed class HallucinationFilter
{
    public const float NoSpeechThreshold = 0.5f;

    private readonly IReadOnlyList<string> _phrases;

    public HallucinationFilter(HallucinationManifest manifest)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        _phrases = manifest.Phrases ?? new List<string>();
    }

    public bool ShouldDrop(string chunkText, float averageNoSpeechProbability)
    {
        if (averageNoSpeechProbability > NoSpeechThreshold) return true;
        if (string.IsNullOrEmpty(chunkText)) return false;
        foreach (var phrase in _phrases)
        {
            if (string.IsNullOrEmpty(phrase)) continue;
            if (chunkText.Contains(phrase, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
