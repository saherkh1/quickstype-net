using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Mac;

public sealed class MacAutoLaunch : IAutoLaunchService
{
    private const string Label = "com.saherk.quickstype";
    private readonly ILogger _log;

    public MacAutoLaunch(ILogger<MacAutoLaunch>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    private static string PlistPath()
    {
        var home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", $"{Label}.plist");
    }

    public bool IsEnabled() => File.Exists(PlistPath());

    public void SetEnabled(bool enabled, string? executablePath = null)
    {
        var plistPath = PlistPath();
        if (!enabled)
        {
            if (File.Exists(plistPath))
            {
                Unload(plistPath);
                try { File.Delete(plistPath); }
                catch (Exception ex) { _log.LogDebug(ex, "Could not delete plist {Path}", plistPath); }
            }
            return;
        }

        var exe = executablePath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine executable path");

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        var plist = $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple Computer//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
          <dict>
            <key>Label</key>
            <string>{Label}</string>
            <key>ProgramArguments</key>
            <array>
              <string>{exe}</string>
            </array>
            <key>RunAtLoad</key>
            <true/>
            <key>KeepAlive</key>
            <false/>
            <key>ProcessType</key>
            <string>Interactive</string>
          </dict>
        </plist>
        """;
        File.WriteAllText(plistPath, plist);
        Load(plistPath);
    }

    private void Load(string plistPath)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("/bin/launchctl")
            {
                ArgumentList = { "load", plistPath },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(2000);
        }
        catch (Exception ex) { _log.LogWarning(ex, "launchctl load failed for {Path}", plistPath); }
    }

    private void Unload(string plistPath)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("/bin/launchctl")
            {
                ArgumentList = { "unload", plistPath },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(2000);
        }
        catch (Exception ex) { _log.LogWarning(ex, "launchctl unload failed for {Path}", plistPath); }
    }
}
