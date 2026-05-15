using System.Text.Json.Serialization;

namespace QuickSType.Core.Transcribe;

public sealed record HallucinationManifest
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("phrases")]
    public List<string> Phrases { get; init; } = new();
}
