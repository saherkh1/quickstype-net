using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using QuickSType.Core.Config;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;
using QuickSType.UI.ViewModels;

namespace QuickSType.UI.Views;

public partial class MainWindow : Window
{
    private readonly AppHost? _host;
    private SettingsViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(AppHost host) : this()
    {
        _host = host;
        _vm = new SettingsViewModel(host);
        DataContext = _vm;
        WireButtons();
        _vm.PropertyChanged += OnVmPropertyChanged;
        Opened += (_, _) => Dispatcher.UIThread.Post(SyncLanguageRadios);
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
        if (tab == MainTab.Languages) Dispatcher.UIThread.Post(SyncLanguageRadios);
    }

    private void LanguageRadio_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (sender is RadioButton rb && rb.Tag is string code && !string.IsNullOrEmpty(code))
        {
            _vm.SelectedLanguageCode = code;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedLanguageCode))
        {
            Dispatcher.UIThread.Post(SyncLanguageRadios);
        }
    }

    private void SyncLanguageRadios()
    {
        if (_vm is null) return;
        var active = _vm.SelectedLanguageCode;

        var auto = this.FindControl<RadioButton>("AutoDetectRadio");
        if (auto is not null) auto.IsChecked = active == SettingsViewModel.AutoLanguageCode;

        var list = this.FindControl<ItemsControl>("LanguageList");
        if (list is null) return;

        foreach (var radio in list.GetLogicalDescendants().OfType<RadioButton>())
        {
            if (radio.Tag is string code)
            {
                radio.IsChecked = string.Equals(code, active, StringComparison.OrdinalIgnoreCase);
            }
        }
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
