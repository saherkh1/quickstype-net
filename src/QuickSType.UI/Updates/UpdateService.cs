using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Config;
using Velopack;

namespace QuickSType.UI.Updates;

public sealed class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _log;

    public UpdateService(ILogger<UpdateService>? log = null)
    {
        _log = log ?? NullLogger<UpdateService>.Instance;
    }

    public UpdateStatus GetCurrentStatus(AppConfig config)
    {
        var managedBy = NormalizePackageManager(config.ManagedPackageManager);
        if (managedBy is not null)
        {
            return new UpdateStatus(
                UpdateStatusKind.ManagedByPackageManager,
                GetFallbackVersion(),
                NormalizeChannel(config.UpdateChannel),
                managedBy);
        }

        if (string.IsNullOrWhiteSpace(config.UpdateSourceUrl))
        {
            return new UpdateStatus(
                UpdateStatusKind.Disabled,
                GetFallbackVersion(),
                NormalizeChannel(config.UpdateChannel),
                Message: "No update source configured.");
        }

        try
        {
            var manager = CreateManager(config);
            var pending = manager.UpdatePendingRestart;
            if (pending is not null)
            {
                return new UpdateStatus(
                    UpdateStatusKind.UpdateReadyToRestart,
                    FormatVersion(manager.CurrentVersion),
                    NormalizeChannel(config.UpdateChannel),
                    Message: $"Update {pending.Version} is ready to install.");
            }

            if (!manager.IsInstalled)
            {
                return new UpdateStatus(
                    UpdateStatusKind.NotInstalled,
                    GetFallbackVersion(),
                    NormalizeChannel(config.UpdateChannel),
                    Message: "Updates are available only from an installed QuickSType package.");
            }

            return new UpdateStatus(
                UpdateStatusKind.UpToDate,
                FormatVersion(manager.CurrentVersion),
                NormalizeChannel(config.UpdateChannel));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to read Velopack update status");
            return ErrorStatus(config, ex);
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        var currentStatus = GetCurrentStatus(config);
        if (currentStatus.Kind is UpdateStatusKind.Disabled
            or UpdateStatusKind.ManagedByPackageManager
            or UpdateStatusKind.NotInstalled
            or UpdateStatusKind.Error)
        {
            return new UpdateCheckResult(currentStatus);
        }

        try
        {
            var manager = CreateManager(config);
            var pending = manager.UpdatePendingRestart;
            if (pending is not null)
            {
                return new UpdateCheckResult(
                    currentStatus with
                    {
                        Kind = UpdateStatusKind.UpdateReadyToRestart,
                        Message = $"Update {pending.Version} is ready to install.",
                    },
                    pending.Version?.ToString())
                {
                    NativeUpdate = pending,
                    SourceUrl = config.UpdateSourceUrl,
                };
            }

            var update = await manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
            if (update is null)
            {
                return new UpdateCheckResult(currentStatus with { Kind = UpdateStatusKind.UpToDate });
            }

            var targetVersion = update.TargetFullRelease.Version?.ToString();
            return new UpdateCheckResult(
                currentStatus with
                {
                    Kind = UpdateStatusKind.UpdateAvailable,
                    Message = targetVersion is null ? "Update available." : $"Update {targetVersion} available.",
                },
                targetVersion)
            {
                NativeUpdate = update,
                SourceUrl = config.UpdateSourceUrl,
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Update check failed");
            return new UpdateCheckResult(ErrorStatus(config, ex));
        }
    }

    public async Task DownloadUpdatesAsync(
        UpdateCheckResult update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (update.NativeUpdate is not UpdateInfo nativeUpdate)
            throw new InvalidOperationException("No Velopack update is available to download.");

        var manager = CreateManagerFromSource(update.SourceUrl, update.Status.Channel);
        await manager.DownloadUpdatesAsync(
            nativeUpdate,
            progress is null ? null : progress.Report,
            cancellationToken);
    }

    public void ApplyUpdatesAndRestart(UpdateCheckResult update, string[]? restartArgs = null)
    {
        var manager = CreateManagerFromSource(update.SourceUrl, update.Status.Channel);
        switch (update.NativeUpdate)
        {
            case UpdateInfo nativeUpdate:
                manager.ApplyUpdatesAndRestart(nativeUpdate, restartArgs);
                break;
            case VelopackAsset pending:
                manager.ApplyUpdatesAndRestart(pending, restartArgs);
                break;
            default:
                throw new InvalidOperationException("No Velopack update is available to apply.");
        }
    }

    private UpdateManager CreateManager(AppConfig config)
    {
        var source = config.UpdateSourceUrl;
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("No update source configured.");

        return CreateManager(source, NormalizeChannel(config.UpdateChannel));
    }

    private static UpdateManager CreateManagerFromSource(string? source, string channel)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("No update source configured.");

        return CreateManager(source, channel);
    }

    private static UpdateManager CreateManager(string source, string channel)
    {
        return new UpdateManager(source, new UpdateOptions { ExplicitChannel = channel });
    }

    private static UpdateStatus ErrorStatus(AppConfig config, Exception ex)
    {
        return new UpdateStatus(
            UpdateStatusKind.Error,
            GetFallbackVersion(),
            NormalizeChannel(config.UpdateChannel),
            Message: ex.Message);
    }

    private static string NormalizeChannel(string? channel)
    {
        return string.IsNullOrWhiteSpace(channel) ? "canary" : channel.Trim().ToLowerInvariant();
    }

    private static string? NormalizePackageManager(string? manager)
    {
        if (string.IsNullOrWhiteSpace(manager)) return null;

        var normalized = manager.Trim().ToLowerInvariant();
        return normalized is "homebrew" or "winget" ? normalized : null;
    }

    private static string? FormatVersion(object? version)
    {
        return version?.ToString() ?? GetFallbackVersion();
    }

    private static string? GetFallbackVersion()
    {
        return typeof(UpdateService).Assembly.GetName().Version?.ToString(3);
    }
}
