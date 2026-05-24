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
    public string Hotkey { get; init; } = "VcRightCtrl";   // HARDEN-02 / D-Q3: was "VcRightAlt"; flipped to dodge AltGr collision on Windows international layouts

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
    public int SchemaVersion { get; init; } = 3;

    [JsonPropertyName("enable_crash_telemetry")]
    public bool EnableCrashTelemetry { get; init; } = false;

    [JsonPropertyName("keyboard_layout_driven")]
    public bool KeyboardLayoutDriven { get; init; } = false;

    [JsonPropertyName("streaming_mode")]
    public string StreamingMode { get; init; } = "auto";

    [JsonPropertyName("enable_streaming_insertion")]
    public bool EnableStreamingInsertion { get; init; } = true;

    [JsonPropertyName("preferred_model")]
    public string? PreferredModel { get; init; } = null;

    [JsonPropertyName("enable_vibrancy")]
    public bool? EnableVibrancy { get; init; } = null;

    [JsonPropertyName("update_channel")]
    public string UpdateChannel { get; init; } = "canary";

    [JsonPropertyName("update_source_url")]
    public string? UpdateSourceUrl { get; init; } = null;

    [JsonPropertyName("managed_package_manager")]
    public string? ManagedPackageManager { get; init; } = null;

    [JsonPropertyName("enable_background_update_checks")]
    public bool EnableBackgroundUpdateChecks { get; init; } = true;

    [JsonPropertyName("language_models")]
    public Dictionary<string, string> LanguageModels { get; init; } = new();

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

    public AppConfig WithAutoLanguage(bool enabled) => this with { AutoLanguage = enabled };

    /// <summary>
    /// Replace the enabled-languages list. Input is normalised (trim, lowercase),
    /// deduplicated, and filtered to known codes in <see cref="QuickSType.Core.Languages.Common"/>.
    /// If the resulting list is empty, falls back to ["en"]. If the current
    /// <see cref="ActiveLanguage"/> is no longer in the list, shifts to the first item.
    /// </summary>
    public AppConfig WithEnabledLanguages(IEnumerable<string> codes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        foreach (var raw in codes ?? Array.Empty<string>())
        {
            var n = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (n.Length == 0) continue;
            if (QuickSType.Core.Languages.Find(n) is null) continue;
            if (seen.Add(n)) ordered.Add(n);
        }
        if (ordered.Count == 0) ordered.Add("en");

        var newActive = ordered.Contains(ActiveLanguage, StringComparer.OrdinalIgnoreCase)
            ? ActiveLanguage
            : ordered[0];

        return this with
        {
            Languages = ordered,
            ActiveLanguage = newActive,
        };
    }

    /// <summary>
    /// Returns a copy of this config with <see cref="PreferredModel"/> set to <paramref name="modelId"/>.
    /// Pass null to clear (e.g., to force the first-run flow on next launch — uncommon, normally only set forward).
    /// </summary>
    public AppConfig WithPreferredModel(string? modelId) => this with { PreferredModel = modelId };

    /// <summary>
    /// Returns a copy of this config with the specified language mapped to <paramref name="modelId"/>.
    /// Pass null modelId to remove the language-specific mapping (fall back to global model).
    /// </summary>
    public AppConfig WithLanguageModel(string langCode, string? modelId)
    {
        var next = new Dictionary<string, string>(LanguageModels, StringComparer.OrdinalIgnoreCase);
        if (modelId is null)
            next.Remove(langCode);
        else
            next[langCode] = modelId;
        return this with { LanguageModels = next };
    }
}
