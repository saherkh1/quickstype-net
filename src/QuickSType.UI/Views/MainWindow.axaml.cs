using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;
using QuickSType.UI.ViewModels;
using Rectangle = System.Drawing.Rectangle;

namespace QuickSType.UI.Views;

public partial class MainWindow : Window
{
    private readonly ILogger _log;

    public MainWindow()
    {
        _log = NullLogger.Instance;
        InitializeComponent();
    }

    public MainWindow(AppHost host, ILogger<MainWindow>? log = null) : this()
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        var vm = new SettingsViewModel(host);
        vm.SetOwnerWindow(this);
        DataContext = vm;
        WireButtons();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void WireButtons()
    {
        var openLogs = this.FindControl<Button>("OpenLogsButton");
        var openConfig = this.FindControl<Button>("OpenConfigButton");
        if (openLogs is not null) openLogs.Click += (_, _) => OpenInFinder(ModelCatalog.ModelsDirectory());
        if (openConfig is not null) openConfig.Click += (_, _) =>
        {
            var dir = System.IO.Path.GetDirectoryName(ConfigStore.DefaultPath());
            if (!string.IsNullOrEmpty(dir)) OpenInFinder(dir);
        };
    }

    public void NavigateTo(MainTab tab)
    {
        var tabs = this.FindControl<TabControl>("MainTabs");
        if (tabs is null) return;
        tabs.SelectedIndex = tab switch
        {
            MainTab.General => 0,
            MainTab.Languages => 1,
            MainTab.Models => 2,
            MainTab.About => 3,
            _ => 0,
        };
    }

    public void PositionNearTray(Rectangle trayRect)
    {
        const int winW = 480;
        const int winH = 620;
        int x, y;

        if (trayRect.IsEmpty)
        {
            var screen = Screens.Primary;
            if (screen is not null)
            {
                var area = screen.WorkingArea;
                x = area.X + area.Width - winW - 16;
                y = area.Y + 16;
            }
            else
            {
                x = 1920 - winW - 16;
                y = 16;
            }
        }
        else
        {
            // Center horizontally under the tray icon, drop 8px below it
            x = trayRect.Left + (trayRect.Width - winW) / 2;
            y = OperatingSystem.IsWindows()
                ? trayRect.Top - winH - 8   // Windows taskbar: rise above
                : trayRect.Bottom + 8;       // macOS menu bar: drop below

            // Clamp so the window doesn't go off-screen
            var screen = Screens.ScreenFromPoint(new PixelPoint(x, y)) ?? Screens.Primary;
            if (screen is not null)
            {
                var area = screen.WorkingArea;
                if (x + winW > area.X + area.Width) x = area.X + area.Width - winW;
                if (x < area.X) x = area.X;
                if (y + winH > area.Y + area.Height) y = area.Y + area.Height - winH;
                if (y < area.Y) y = area.Y;
            }
        }

        Position = new PixelPoint(x, y);
    }

    private void OpenInFinder(string path)
    {
        try
        {
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
            if (System.OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("/usr/bin/open", path) { UseShellExecute = false });
            else if (System.OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex) { _log.LogWarning(ex, "OpenInFinder failed for {Path}", path); }
    }
}

public enum MainTab
{
    General,
    Languages,
    Models,
    About,
}
