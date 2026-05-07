using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.Platform.Windows;

public sealed class WindowsPermissions : IPermissionService
{
    private readonly ILogger _log;

    public WindowsPermissions(ILogger<WindowsPermissions>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public bool HasMicrophoneAccess() => true;
    public bool HasInputMonitoringAccess() => true;
    public bool HasAccessibilityAccess() => true;

    public void OpenMicrophoneSettings() => OpenSettings("ms-settings:privacy-microphone");
    public void OpenInputMonitoringSettings() => OpenSettings("ms-settings:privacy");
    public void OpenAccessibilitySettings() => OpenSettings("ms-settings:easeofaccess");

    public void RequestAccessibilityIfNeeded() { /* no-op on Windows */ }

    private void OpenSettings(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex) { _log.LogWarning(ex, "Failed to open ms-settings URI {Uri}", uri); }
    }
}
