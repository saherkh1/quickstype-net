using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace QuickSType.UI.Telemetry;

public static partial class TelemetryScrubber
{
    private const string Redacted = "[redacted]";

    private static readonly string[] SensitiveKeyFragments =
    [
        "transcript",
        "transcribed",
        "dictation",
        "utterance",
        "recognized_text",
        "audio_device",
        "microphone",
        "device_name",
        "model_path",
        "model_file",
        "path",
        "file",
        "directory",
        "home",
        "username",
        "user_name",
    ];

    public static string Scrub(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var scrubbed = value;
        scrubbed = EmailRegex().Replace(scrubbed, Redacted);
        scrubbed = MacUserPathRegex().Replace(scrubbed, "$1" + Redacted);
        scrubbed = WindowsUserPathRegex().Replace(scrubbed, "$1" + Redacted);
        scrubbed = AudioDeviceAssignmentRegex().Replace(scrubbed, "$1" + Redacted);
        scrubbed = ModelFileRegex().Replace(scrubbed, Redacted);
        scrubbed = AudioDevicePhraseRegex().Replace(scrubbed, "$1 " + Redacted);
        scrubbed = TranscribedTextRegex().Replace(scrubbed, "$1" + Redacted);

        return scrubbed;
    }

    public static IReadOnlyDictionary<string, string> Scrub(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        var scrubbed = new Dictionary<string, string>(values.Count, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            scrubbed[key] = IsSensitiveKey(key) ? Redacted : Scrub(value);
        }

        return scrubbed;
    }

    public static IReadOnlyDictionary<string, object?> Scrub(IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null || values.Count == 0)
        {
            return ReadOnlyDictionary<string, object?>.Empty;
        }

        var scrubbed = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            scrubbed[key] = IsSensitiveKey(key) ? Redacted : ScrubValue(value);
        }

        return scrubbed;
    }

    public static object? ScrubValue(object? value) => value switch
    {
        null => null,
        string text => Scrub(text),
        IReadOnlyDictionary<string, string> textMap => Scrub(textMap),
        IReadOnlyDictionary<string, object?> objectMap => Scrub(objectMap),
        _ => value,
    };

    private static bool IsSensitiveKey(string key)
    {
        var normalized = KeySeparatorRegex().Replace(key, "_");
        foreach (var fragment in SensitiveKeyFragments)
        {
            if (normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(/Users/)[^/\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex MacUserPathRegex();

    [GeneratedRegex(@"(\b[A-Za-z]:\\Users\\)[^\\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsUserPathRegex();

    [GeneratedRegex(@"(?:[A-Za-z]:\\|/)[^\s'""<>]*(?:ggml-[^\s'""<>\\/]+|whisper|models?)[^\s'""<>]*(?:\.bin|\.onnx|\.gguf)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModelFileRegex();

    [GeneratedRegex(@"(\b(?:audio device|microphone|device)\s*[:=]\s*)(?:""[^""]*""|'[^']*(?:'s [^']*)?'|[^.;,\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AudioDeviceAssignmentRegex();

    [GeneratedRegex(@"(\b(?:audio device|microphone|device)\s+(?:named|name|was|is))\s+[^.;,\r\n]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AudioDevicePhraseRegex();

    [GeneratedRegex(@"(\b(?:transcribed|transcript|dictated|recognized text)\s*[:=]\s*)(?:""[^""]*""|'[^']*'|[^.;\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TranscribedTextRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex KeySeparatorRegex();
}
