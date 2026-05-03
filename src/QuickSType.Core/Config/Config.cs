using System.Text.Json.Serialization;

namespace QuickSType.Core.Config;

public sealed record AppConfig
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "ggml-base";

    [JsonPropertyName("languages")]
    public List<string> Languages { get; init; } = new() { "en" };

    [JsonPropertyName("active_language")]
    public string ActiveLanguage { get; init; } = "en";

    [JsonPropertyName("hotkey")]
    public string Hotkey { get; init; } = "VcRightAlt";

    [JsonPropertyName("auto_language")]
    public bool AutoLanguage { get; init; }

    [JsonPropertyName("start_at_login")]
    public bool StartAtLogin { get; init; }

    [JsonPropertyName("selected_audio_device")]
    public string? SelectedAudioDevice { get; init; }

    [JsonPropertyName("transcription_backend")]
    public string TranscriptionBackend { get; init; } = "auto";

    [JsonPropertyName("show_notifications")]
    public bool ShowNotifications { get; init; } = true;

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = 2;

    public AppConfig WithLanguage(string lang)
    {
        var normalised = (lang ?? string.Empty).Trim().ToLowerInvariant();
        if (normalised.Length == 0) return this;
        if (QuickSType.Core.Languages.Find(normalised) is null) return this;
        return this with
        {
            ActiveLanguage = normalised,
            Languages = Languages.Contains(normalised) ? Languages : [.. Languages, normalised],
            AutoLanguage = false,
        };
    }

    public AppConfig WithAutoLanguage() => this with { AutoLanguage = true };
}
