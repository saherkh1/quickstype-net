using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;
using QuickSType.UI.ViewModels;

namespace QuickSType.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(AppHost host) : this()
    {
        DataContext = new SettingsViewModel(host);
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

    private static void OpenInFinder(string path)
    {
        try
        {
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
            if (System.OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("/usr/bin/open", path) { UseShellExecute = false });
            else if (System.OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch { /* swallow */ }
    }
}

public enum MainTab
{
    General,
    Languages,
    Models,
    About,
}
