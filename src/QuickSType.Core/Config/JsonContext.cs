using System.Text.Json.Serialization;

namespace QuickSType.Core.Config;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(LegacyPythonConfig))]
[JsonSerializable(typeof(QuickSType.Core.Transcribe.ModelManifest))]
public partial class ConfigJsonContext : JsonSerializerContext
{
}

public sealed record LegacyPythonConfig
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("languages")]
    public List<string>? Languages { get; init; }

    [JsonPropertyName("hotkey")]
    public string? Hotkey { get; init; }

    [JsonPropertyName("auto_language")]
    public bool? AutoLanguage { get; init; }
}
