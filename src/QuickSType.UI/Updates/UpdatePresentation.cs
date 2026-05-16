using QuickSType.Core;

namespace QuickSType.UI.Updates;

public static class UpdatePresentation
{
    public static UpdateUiState FromStatus(
        UpdateStatus status,
        DateTimeOffset? lastChecked = null,
        bool isBusy = false,
        DictationState dictationState = DictationState.Idle)
    {
        if (isBusy)
        {
            return new UpdateUiState(
                TrayHeader: "Checking for Updates...",
                SettingsStatus: "Checking for updates...",
                ActionText: "Checking...",
                IsActionEnabled: false,
                LastCheckedText: FormatLastChecked(lastChecked));
        }

        var last = FormatLastChecked(lastChecked);
        return status.Kind switch
        {
            UpdateStatusKind.ManagedByPackageManager => Managed(status, last),
            UpdateStatusKind.Disabled => new UpdateUiState(
                "Check for Updates...",
                status.Message ?? "Updates are not configured.",
                "Check for Updates",
                false,
                last),
            UpdateStatusKind.NotInstalled => new UpdateUiState(
                "Check for Updates...",
                status.Message ?? "Install QuickSType before using in-app updates.",
                "Check for Updates",
                false,
                last),
            UpdateStatusKind.UpdateAvailable => new UpdateUiState(
                "Install Update and Restart",
                status.Message ?? "Update available.",
                "Install and Restart",
                dictationState == DictationState.Idle,
                last),
            UpdateStatusKind.UpdateReadyToRestart => new UpdateUiState(
                "Install Update and Restart",
                status.Message ?? "Update ready to install.",
                "Install and Restart",
                dictationState == DictationState.Idle,
                last),
            UpdateStatusKind.Error => new UpdateUiState(
                "Check for Updates...",
                string.IsNullOrWhiteSpace(status.Message) ? "Update check failed." : $"Update check failed: {status.Message}",
                "Check for Updates",
                true,
                last),
            _ => new UpdateUiState(
                "Check for Updates...",
                "QuickSType is up to date.",
                "Check for Updates",
                true,
                last),
        };
    }

    public static string FormatVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? "Version unknown" : $"Version {version}";

    public static string FormatChannel(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? "Channel: canary" : $"Channel: {channel}";

    private static UpdateUiState Managed(UpdateStatus status, string lastChecked)
    {
        var manager = status.ManagedPackageManager switch
        {
            "homebrew" => "Homebrew",
            "winget" => "Winget",
            var other when !string.IsNullOrWhiteSpace(other) => other,
            _ => "your package manager",
        };

        return new UpdateUiState(
            $"Updates managed by {manager}",
            $"Updates managed by {manager}.",
            $"Managed by {manager}",
            false,
            lastChecked);
    }

    private static string FormatLastChecked(DateTimeOffset? lastChecked)
    {
        return lastChecked is null ? "Last checked: never" : $"Last checked: {lastChecked:yyyy-MM-dd HH:mm}";
    }
}

public sealed record UpdateUiState(
    string TrayHeader,
    string SettingsStatus,
    string ActionText,
    bool IsActionEnabled,
    string LastCheckedText);
