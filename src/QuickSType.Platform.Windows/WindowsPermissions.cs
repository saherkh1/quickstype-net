using QuickSType.Core.Platform;

namespace QuickSType.Platform.Windows;

public sealed class WindowsPermissions : IPermissionService
{
    public bool HasMicrophoneAccess() => true;
    public bool HasInputMonitoringAccess() => true;
    public bool HasAccessibilityAccess() => true;

    public void OpenMicrophoneSettings() => OpenSettings("ms-settings:privacy-microphone");
    public void OpenInputMonitoringSettings() => OpenSettings("ms-settings:privacy");
    public void OpenAccessibilitySettings() => OpenSettings("ms-settings:easeofaccess");

    public void RequestAccessibilityIfNeeded() { /* no-op on Windows */ }

    private static void OpenSettings(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch { /* swallow */ }
    }
}
