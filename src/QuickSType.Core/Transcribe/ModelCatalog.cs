namespace QuickSType.Core.Transcribe;

public sealed record ModelInfo(
    string Id,
    string DisplayName,
    string Url,
    long ApproxSizeBytes,
    string Description);

public static class ModelCatalog
{
    private const string Hf = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";

    public static readonly IReadOnlyList<ModelInfo> All = new ModelInfo[]
    {
        new("ggml-tiny",                 "Tiny (75 MB)",            $"{Hf}/ggml-tiny.bin",                  78_000_000,    "Smallest. Fast on any machine, lower accuracy."),
        new("ggml-base",                 "Base (142 MB)",           $"{Hf}/ggml-base.bin",                  148_000_000,   "Good baseline. Works on most machines."),
        new("ggml-small",                "Small (466 MB)",          $"{Hf}/ggml-small.bin",                 488_000_000,   "Better accuracy, moderate speed."),
        new("ggml-medium",               "Medium (1.5 GB)",         $"{Hf}/ggml-medium.bin",                1_530_000_000, "High accuracy, slower."),
        new("ggml-large-v3",             "Large v3 (3.1 GB)",       $"{Hf}/ggml-large-v3.bin",              3_100_000_000, "Best accuracy. Heaviest."),
        new("ggml-large-v3-turbo",       "Large v3 Turbo (1.6 GB)", $"{Hf}/ggml-large-v3-turbo.bin",        1_620_000_000, "Newer turbo variant. Faster than large-v3 with similar accuracy."),
        new("ggml-large-v3-turbo-q5_0",  "Large v3 Turbo Q5 (570 MB) — recommended", $"{Hf}/ggml-large-v3-turbo-q5_0.bin", 574_000_000, "Quantised turbo. Half the size, near-identical accuracy. Default."),
    };

    public static ModelInfo? Find(string id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    public static ModelInfo Default => Find("ggml-large-v3-turbo-q5_0") ?? All[1];

    public static string ModelsDirectory()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            var home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "QuickSType", "models");
        }
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "QuickSType", "models");
        }
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(data, "quickstype", "models");
    }

    public static string PathFor(string modelId) =>
        Path.Combine(ModelsDirectory(), modelId + ".bin");

    public static bool IsInstalled(string modelId) => File.Exists(PathFor(modelId));

    public static string DenyListPathFor(string modelId) =>
        Path.Combine(ModelsDirectory(), modelId + ".hallucinations.json");

    public static string DenyListUrlFor(string modelId) =>
        $"{Hf}/{modelId}.hallucinations.json";
}
