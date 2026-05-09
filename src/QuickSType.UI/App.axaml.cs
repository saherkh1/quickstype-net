using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using QuickSType.UI.Composition;
using QuickSType.UI.Tray;

namespace QuickSType.UI;

public partial class App : Application
{
    public AppHost? Host { get; private set; }
    public TrayService? Tray { get; private set; }
    private Views.BlockerDialogWindow? _blocker;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Host = AppHost.Create();

            // CONFIG-03 / D-04: pre-tray hardware check. If the system can't run any model,
            // show the blocker dialog INSTEAD of installing the tray. The blocker controls
            // the rest of startup via the onOpenSettings callback or Environment.Exit.
            if (Host.SystemSpecsService.IsBlocker(Host.SystemSpecs))
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
                desktop.MainWindow = null;

                Host.LoggerFactory.CreateLogger<App>().LogWarning(
                    "Hardware spec check failed: RAM={RamGb:F1} GB, FreeDisk={DiskGb:F1} GB; showing blocker dialog",
                    Host.SystemSpecs.TotalRamGb, Host.SystemSpecs.FreeDiskGb);

                _blocker = new Views.BlockerDialogWindow(
                    Host.SystemSpecs,
                    Host.SystemSpecsService.MinRamGb,
                    Host.SystemSpecsService.MinFreeDiskGb,
                    onOpenSettings: () => OnBlockerOpenSettings(),
                    log: Host.LoggerFactory.CreateLogger<Views.BlockerDialogWindow>());
                _blocker.Show();

                base.OnFrameworkInitializationCompleted();
                return; // Tray NOT installed; blocker callback handles it on Open Settings.
            }

            // Normal path (existing): install tray, no MainWindow until user opens it.
            Tray = new TrayService(Host);
            Tray.Install(this);

            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = null;

            _ = Host.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnBlockerOpenSettings()
    {
        if (Host is null) return;

        try
        {
            Host.LoggerFactory.CreateLogger<App>().LogInformation("Blocker: OpenSettings callback invoked — installing tray and opening MainWindow on Models tab");

            Tray = new TrayService(Host);
            Tray.Install(this);
            _ = Host.StartAsync();

            var mainWindow = new Views.MainWindow(Host, Host.LoggerFactory.CreateLogger<Views.MainWindow>());
            mainWindow.NavigateTo(Views.MainTab.Models);
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Host.LoggerFactory.CreateLogger<App>().LogError(ex, "Blocker: OnBlockerOpenSettings failed");
            Environment.Exit(1);
        }
    }
}
