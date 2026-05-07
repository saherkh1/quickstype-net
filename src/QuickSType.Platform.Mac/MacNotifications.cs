using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

public sealed class MacNotifications : INotificationService
{
    private const string Osascript = "/usr/bin/osascript";
    private const string Afplay = "/usr/bin/afplay";
    private const string SoundDir = "/System/Library/Sounds";
    private const string StartSound = "Tink.aiff";
    private const string StopSound = "Pop.aiff";
    private readonly ILogger _log;

    public MacNotifications(ILogger<MacNotifications>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public void Notify(string title, string message, string? subtitle = null)
    {
        var t = Escape(title);
        var m = Escape(message);
        var sub = subtitle is null ? string.Empty : $" subtitle \"{Escape(subtitle)}\"";
        var script = $"display notification \"{m}\" with title \"{t}\"{sub}";
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = Osascript,
                ArgumentList = { "-e", script },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            p?.WaitForExit(1500);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "osascript display notification failed for {Title}", title);
        }
    }

    public Task PlayStartAsync() => PlaySoundAsync(StartSound);
    public Task PlayStopAsync() => PlaySoundAsync(StopSound);

    private Task PlaySoundAsync(string fileName)
    {
        var path = System.IO.Path.Combine(SoundDir, fileName);
        if (!File.Exists(path)) return Task.CompletedTask;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Afplay,
                ArgumentList = { path },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var p = Process.Start(psi);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "afplay failed for {Path}", path);
            return Task.CompletedTask;
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
