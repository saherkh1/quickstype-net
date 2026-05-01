using Avalonia;
using Avalonia.Controls;
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

    public TrayService(AppHost host)
    {
        _host = host;
        _host.Engine.StateChanged += OnStateChanged;
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

        var settings = new NativeMenuItem("Settings…");
        settings.Click += (_, _) => ShowSettings();
        menu.Add(settings);

        menu.Add(new NativeMenuItemSeparator());

        _languageRoot = new NativeMenuItem("Language") { Menu = BuildLanguageSubmenu() };
        menu.Add(_languageRoot);

        menu.Add(new NativeMenuItemSeparator());

        var about = new NativeMenuItem("About QuickSType");
        about.Click += (_, _) => ShowAbout();
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
            RebuildLanguageMenu();
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
                RebuildLanguageMenu();
            };
            sub.Add(item);
        }

        sub.Add(new NativeMenuItemSeparator());
        var manage = new NativeMenuItem("Manage languages…");
        manage.Click += (_, _) => ShowSettings(focusLanguages: true);
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

    private static SettingsWindow? _settingsWindow;
    private void ShowSettings(bool focusLanguages = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_settingsWindow is null || !_settingsWindow.IsVisible)
            {
                _settingsWindow = new SettingsWindow(_host)
                {
                    DataContext = new ViewModels.SettingsViewModel(_host),
                };
                _settingsWindow.Closed += (_, _) =>
                {
                    _settingsWindow = null;
                    RebuildLanguageMenu();
                };
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
            if (focusLanguages) _settingsWindow.FocusLanguages();
        });
    }

    private void ShowAbout()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var about = new AboutWindow();
            about.Show();
        });
    }

    private void Quit()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
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
