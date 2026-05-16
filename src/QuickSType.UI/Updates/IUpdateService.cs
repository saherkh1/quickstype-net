using QuickSType.Core.Config;

namespace QuickSType.UI.Updates;

public interface IUpdateService
{
    UpdateStatus GetCurrentStatus(AppConfig config);

    Task<UpdateCheckResult> CheckForUpdatesAsync(
        AppConfig config,
        CancellationToken cancellationToken = default);

    Task DownloadUpdatesAsync(
        UpdateCheckResult update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    void ApplyUpdatesAndRestart(UpdateCheckResult update, string[]? restartArgs = null);
}

public enum UpdateStatusKind
{
    Disabled,
    ManagedByPackageManager,
    NotInstalled,
    UpToDate,
    UpdateAvailable,
    UpdateReadyToRestart,
    Error,
}

public sealed record UpdateStatus(
    UpdateStatusKind Kind,
    string? CurrentVersion,
    string Channel,
    string? ManagedPackageManager = null,
    string? Message = null);

public sealed record UpdateCheckResult(
    UpdateStatus Status,
    string? TargetVersion = null)
{
    internal object? NativeUpdate { get; init; }
    internal string? SourceUrl { get; init; }
}
