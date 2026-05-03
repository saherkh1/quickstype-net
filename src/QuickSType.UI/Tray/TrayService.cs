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
    private const string AutoLangKey = "__auto";

    private readonly AppHost _host;
    private TrayIcon? _trayIcon;
    private NativeMenu? _menu;
    private NativeMenuItem? _stateItem;
    private NativeMenuItem? _languageRoot;
    private NativeMenuItem? _autoLangItem;
    private readonly Dictionary<string, NativeMenuItem> _langItems = new(StringComparer.OrdinalIgnoreCase);
    private MainWindow? _mainWindow;

    public TrayService(AppHost host)
    {
        _host = host;
        _host.Engine.StateChanged += OnStateChanged;
        _host.ConfigChanged += _ => Dispatcher.UIThread.Post(RefreshLanguageMarks);
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

        // Build the Language submenu ONCE — every supported language gets a
        // permanent item. We only mutate each item's Header (✓/space prefix)
        // when the active language changes. Replacing NativeMenuItem.Menu at
        // runtime does NOT reliably propagate to AppKit's NSMenu cache, so we
        // avoid that pattern entirely.
        _languageRoot = new NativeMenuItem("Language") { Menu = BuildLanguageMenuOnce() };
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

    private NativeMenu BuildLanguageMenuOnce()
    {
        var sub = new NativeMenu();

        _autoLangItem = new NativeMenuItem(FormatLangHeader("Auto-detect", _host.Config.AutoLanguage));
        _autoLangItem.Click += (_, _) =>
        {
            _host.UpdateConfig(_host.Config.WithAutoLanguage());
        };
        sub.Add(_autoLangItem);
        sub.Add(new NativeMenuItemSeparator());

        foreach (var info in Languages.Common)
        {
            var label = $"{info.NativeName}  ({info.DisplayName})";
            var checkedNow = !_host.Config.AutoLanguage
                && string.Equals(info.Code, _host.Config.ActiveLanguage, StringComparison.OrdinalIgnoreCase);
            var item = new NativeMenuItem(FormatLangHeader(label, checkedNow));
            var capturedCode = info.Code;
            item.Click += (_, _) =>
            {
                _host.UpdateConfig(_host.Config.WithLanguage(capturedCode));
            };
            _langItems[info.Code] = item;
            sub.Add(item);
        }

        sub.Add(new NativeMenuItemSeparator());
        var manage = new NativeMenuItem("Open settings…");
        manage.Click += (_, _) => ShowMain(MainTab.Languages);
        sub.Add(manage);

        return sub;
    }

    private void RefreshLanguageMarks()
    {
        if (_autoLangItem is not null)
        {
            _autoLangItem.Header = FormatLangHeader("Auto-detect", _host.Config.AutoLanguage);
        }

        foreach (var (code, item) in _langItems)
        {
            var info = Languages.Find(code);
            var label = info is null ? code : $"{info.NativeName}  ({info.DisplayName})";
            var checkedNow = !_host.Config.AutoLanguage
                && string.Equals(code, _host.Config.ActiveLanguage, StringComparison.OrdinalIgnoreCase);
            item.Header = FormatLangHeader(label, checkedNow);
        }
    }

    private static string FormatLangHeader(string label, bool isActive) =>
        (isActive ? "✓ " : "   ") + label;

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
