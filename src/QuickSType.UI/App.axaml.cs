using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using QuickSType.Core;
using QuickSType.UI.Composition;
using QuickSType.UI.Tray;

namespace QuickSType.UI;

public partial class App : Application
{
    public AppHost? Host { get; private set; }
    public TrayService? Tray { get; private set; }
    private Views.BlockerDialogWindow? _blocker;
    private Views.HudWindow? _hudWindow;
    private ViewModels.HudViewModel? _hudVm;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Host = AppHost.Create();

            // PLAT-05: subscribe to system theme before any UI is shown.
            // Apply initial theme, then listen for OS-level changes.
            var initialTheme = Host.SystemTheme.Current;
            Application.Current!.RequestedThemeVariant = initialTheme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            Host.SystemTheme.Changed += newTheme =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Application.Current!.RequestedThemeVariant = newTheme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
                });
            };

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

            // Close HUD when app exits.
            desktop.Exit += (_, _) => _hudWindow?.Close();

            // HUD lifecycle: create one HudViewModel + HudWindow pair for the app lifetime.
            _hudVm = new ViewModels.HudViewModel();
            _hudWindow = new Views.HudWindow(Host.LoggerFactory.CreateLogger<Views.HudWindow>());
            _hudWindow.DataContext = _hudVm;

            // Apply initial vibrancy from config.
            _hudVm.UpdateVibrancy(Host.ResolveVibrancyEnabled());

            // Wire real microphone RMS into the HUD waveform.
            _hudWindow.AttachAudioLevels(Host.Audio);

            // Subscribe to DictationEngine state changes and marshal to UI thread.
            Host.Engine.StateChanged += state =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (state is DictationState.Recording or DictationState.Streaming)
                        {
                            _hudVm.OnStateChanged(state);
                            var trayRect = Host.TrayPosition.GetTrayRect();
                            _hudWindow.PositionNearTray(trayRect);
                            if (!_hudWindow.IsVisible) _hudWindow.Show();
                        }
                        else if (state == DictationState.Idle)
                        {
                            _hudVm.OnStateChanged(state);
                            // Hide with brief delay so user sees it go away cleanly.
                            Task.Delay(150).ContinueWith(_ =>
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    if (!_hudVm.IsActive) _hudWindow.Hide();
                                });
                            }, TaskScheduler.Default);
                        }
                        else
                        {
                            // Processing state: update VM but keep HUD visible.
                            _hudVm.OnStateChanged(state);
                        }
                    }
                    catch (Exception ex)
                    {
                        Host.LoggerFactory.CreateLogger<App>().LogError(ex, "HUD StateChanged handler threw");
                    }
                });
            };

            // Subscribe to TranscriptUpdate for HUD preview.
            Host.Engine.TranscriptUpdate += update =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try { _hudVm.OnTranscriptUpdate(update); }
                    catch (Exception ex)
                    {
                        Host.LoggerFactory.CreateLogger<App>().LogError(ex, "HUD TranscriptUpdate handler threw");
                    }
                });
            };

            // Subscribe to config changes so vibrancy updates live.
            Host.ConfigChanged += cfg =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _hudVm.UpdateVibrancy(Host.ResolveVibrancyEnabled());
                });
            };

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
