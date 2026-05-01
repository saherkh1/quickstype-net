namespace QuickSType.Core.Platform;

public interface IPermissionService
{
    bool HasMicrophoneAccess();
    bool HasInputMonitoringAccess();
    bool HasAccessibilityAccess();

    void OpenMicrophoneSettings();
    void OpenInputMonitoringSettings();
    void OpenAccessibilitySettings();

    void RequestAccessibilityIfNeeded();
}
