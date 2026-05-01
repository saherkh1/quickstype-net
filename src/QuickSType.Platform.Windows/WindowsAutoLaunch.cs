using Microsoft.Win32;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Windows;

public sealed class WindowsAutoLaunch : IAutoLaunchService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickSType";

    public bool IsEnabled()
    {
#pragma warning disable CA1416
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string s && !string.IsNullOrEmpty(s);
#pragma warning restore CA1416
    }

    public void SetEnabled(bool enabled, string? executablePath = null)
    {
#pragma warning disable CA1416
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (key is null) return;
        if (enabled)
        {
            var exe = executablePath ?? Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine executable path");
            key.SetValue(ValueName, $"\"{exe}\" --tray");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
#pragma warning restore CA1416
    }
}
