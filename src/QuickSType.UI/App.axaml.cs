using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QuickSType.UI.Composition;
using QuickSType.UI.Tray;

namespace QuickSType.UI;

public partial class App : Application
{
    public AppHost? Host { get; private set; }
    public TrayService? Tray { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Host = AppHost.Create();
            Tray = new TrayService(Host);
            Tray.Install(this);

            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = null;

            _ = Host.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
