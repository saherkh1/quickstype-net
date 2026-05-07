using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Windows;

public sealed partial class WindowsNotifications : INotificationService
{
    private readonly ILogger _log;

    public WindowsNotifications(ILogger<WindowsNotifications>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public void Notify(string title, string message, string? subtitle = null)
    {
        var body = subtitle is null ? message : $"{subtitle}\n{message}";
        var ps = $"""
        Add-Type -AssemblyName System.Windows.Forms;
        $n = New-Object System.Windows.Forms.NotifyIcon;
        $n.Icon = [System.Drawing.SystemIcons]::Information;
        $n.BalloonTipTitle = "{Escape(title)}";
        $n.BalloonTipText = "{Escape(body)}";
        $n.Visible = $true;
        $n.ShowBalloonTip(3000);
        Start-Sleep -Seconds 4;
        $n.Dispose();
        """;
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{ps.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
        }
        catch (Exception ex) { _log.LogWarning(ex, "powershell.exe NotifyIcon spawn failed for {Title}", title); }
    }

    public Task PlayStartAsync()
    {
        try { Beep(880, 80); } catch (Exception ex) { _log.LogTrace(ex, "Beep(start) failed"); }
        return Task.CompletedTask;
    }

    public Task PlayStopAsync()
    {
        try { Beep(440, 80); } catch (Exception ex) { _log.LogTrace(ex, "Beep(stop) failed"); }
        return Task.CompletedTask;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Beep(uint dwFreq, uint dwDuration);
}
