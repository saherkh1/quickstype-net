using System.Text.Json.Serialization;

namespace QuickSType.Core.History;

/// <summary>
/// Append-only JSONL history line. v1 schema (D-07).
/// Field names and snake_case casing are CONTRACT — do not change without a schema-version bump.
/// Phase v1.1 history viewer reads back this exact format.
/// </summary>
public sealed record HistoryEntry
{
    [JsonPropertyName("v")]
    public int V { get; init; } = 1;

    [JsonPropertyName("ts")]
    public string Ts { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("lang")]
    public string Lang { get; init; } = string.Empty;

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; init; }

    [JsonPropertyName("device")]
    public string? Device { get; init; }
}
