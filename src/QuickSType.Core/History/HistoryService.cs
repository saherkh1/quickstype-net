using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Config;
using IoPath = System.IO.Path;

namespace QuickSType.Core.History;

/// <summary>
/// Append-only JSONL writer for dictation history (CONFIG-05).
/// One <see cref="HistoryEntry"/> per line, source-gen JSON, file at
/// platform-specific path sibling to <see cref="QuickSType.Core.Transcribe.ModelCatalog.ModelsDirectory"/>.
/// I/O failures are logged and swallowed — history is best-effort.
/// </summary>
public sealed class HistoryService : IHistoryService
{
    private readonly ILogger _log;
    private readonly string _path;

    public HistoryService(ILogger<HistoryService>? log = null, string? overridePath = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _path = overridePath ?? DefaultPath();
    }

    public string FilePath => _path;

    public static string DefaultPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetEnvironmentVariable("HOME")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return IoPath.Combine(home, "Library", "Application Support", "QuickSType", "history.jsonl");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return IoPath.Combine(local, "QuickSType", "history.jsonl");
        }
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? IoPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");
        return IoPath.Combine(xdg, "quickstype", "history.jsonl");
    }

    public async Task AppendAsync(HistoryEntry entry, CancellationToken ct = default)
    {
        try
        {
            var dir = IoPath.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Source-gen JSON; ConfigJsonContext sets WriteIndented=true globally,
            // so we strip newlines to keep the file as compact JSONL (one entry per line).
            var json = JsonSerializer.Serialize(entry, ConfigJsonContext.Default.HistoryEntry);
            var line = json.Replace("\r", "").Replace("\n", "");
            await File.AppendAllTextAsync(_path, line + "\n", Encoding.UTF8, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to append history entry to {Path}", _path);
        }
    }
}
