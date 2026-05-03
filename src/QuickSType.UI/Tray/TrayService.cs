using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using QuickSType.Core;
using QuickSType.UI.Composition;
using QuickSType.UI.Views;

namespace QuickSType.UI.Tray;

public sealed class TrayService
{
    private readonly AppHost _host;
    private TrayIcon? _trayIcon;
    private NativeMenu? _menu;
    private NativeMenuItem? _stateItem;
    private NativeMenuItem? _languageRoot;
    private MainWindow? _mainWindow;

    public TrayService(AppHost host)
    {
        _host = host;
        _host.Engine.StateChanged += OnStateChanged;
        _host.ConfigChanged += _ => Dispatcher.UIThread.Post(RebuildLanguageMenu);
    }

    public void Install(Application app)
    {
        _menu = BuildMenu();
        _trayIcon = new TrayIcon
        {
            Icon = LoadIcon("idle"),
            ToolTipText = "QuickSType",
            Menu = _menu,
            IsVisible = true,
        };

        var icons = new TrayIcons { _trayIcon };
        TrayIcon.SetIcons(app, icons);
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        _stateItem = new NativeMenuItem("● Idle") { IsEnabled = false };
        menu.Add(_stateItem);
        menu.Add(new NativeMenuItemSeparator());

        var settings = new NativeMenuItem("Open QuickSType…");
        settings.Click += (_, _) => ShowMain(MainTab.General);
        menu.Add(settings);

        menu.Add(new NativeMenuItemSeparator());

        _languageRoot = new NativeMenuItem("Language") { Menu = BuildLanguageSubmenu() };
        menu.Add(_languageRoot);

        menu.Add(new NativeMenuItemSeparator());

        var models = new NativeMenuItem("Models…");
        models.Click += (_, _) => ShowMain(MainTab.Models);
        menu.Add(models);

        var about = new NativeMenuItem("About QuickSType");
        about.Click += (_, _) => ShowMain(MainTab.About);
        menu.Add(about);

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => Quit();
        menu.Add(quit);

        return menu;
    }

    public void RebuildLanguageMenu()
    {
        if (_languageRoot is null) return;
        _languageRoot.Menu = BuildLanguageSubmenu();
    }

    private NativeMenu BuildLanguageSubmenu()
    {
        var sub = new NativeMenu();
        var auto = new NativeMenuItem(_host.Config.AutoLanguage ? "✓ Auto-detect" : "  Auto-detect");
        auto.Click += (_, _) =>
        {
            var cfg = _host.Config.WithAutoLanguage();
            _host.UpdateConfig(cfg);
        };
        sub.Add(auto);
        sub.Add(new NativeMenuItemSeparator());

        foreach (var code in _host.Config.Languages)
        {
            var info = Languages.Find(code);
            var label = info is null ? code : $"{info.NativeName}  ({info.DisplayName})";
            var prefix = (!_host.Config.AutoLanguage && code == _host.Config.ActiveLanguage) ? "✓ " : "  ";
            var item = new NativeMenuItem(prefix + label);
            var capturedCode = code;
            item.Click += (_, _) =>
            {
                var cfg = _host.Config.WithLanguage(capturedCode);
                _host.UpdateConfig(cfg);
            };
            sub.Add(item);
        }

        sub.Add(new NativeMenuItemSeparator());
        var manage = new NativeMenuItem("Manage languages…");
        manage.Click += (_, _) => ShowMain(MainTab.Languages);
        sub.Add(manage);

        return sub;
    }

    private void OnStateChanged(DictationState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_stateItem is not null)
            {
                _stateItem.Header = state switch
                {
                    DictationState.Idle => "● Idle",
                    DictationState.Recording => "● Recording…",
                    DictationState.Processing => "● Transcribing…",
                    _ => "● Idle",
                };
            }
            if (_trayIcon is not null)
            {
                _trayIcon.Icon = LoadIcon(state switch
                {
                    DictationState.Recording => "recording",
                    DictationState.Processing => "processing",
                    _ => "idle",
                });
            }
        });
    }

    private void ShowMain(MainTab tab)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow is null)
            {
                _mainWindow = new MainWindow(_host);
                _mainWindow.Closed += (_, _) => _mainWindow = null;
            }

            if (!_mainWindow.IsVisible) _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.NavigateTo(tab);

            _mainWindow.Topmost = true;
            _mainWindow.Activate();
            _mainWindow.Topmost = false;
            _mainWindow.Focus();
        });
    }

    private void Quit()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }

    private static WindowIcon? LoadIcon(string name)
    {
        try
        {
            var uri = new Uri($"avares://QuickSType/Assets/tray-{name}.png");
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }
}
