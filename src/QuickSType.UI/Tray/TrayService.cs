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
    private NativeMenu? _languageSubmenu;
    private NativeMenuItem? _autoLangItem;
    private NativeMenuItem? _openLangSettingsItem;
    private NativeMenuItemSeparator? _afterAutoSeparator;
    private NativeMenuItemSeparator? _beforeSettingsSeparator;
    private readonly Dictionary<string, NativeMenuItem> _allLangItems = new(StringComparer.OrdinalIgnoreCase);
    private MainWindow? _mainWindow;

    public TrayService(AppHost host)
    {
        _host = host;
        _host.Engine.StateChanged += OnStateChanged;
        _host.ConfigChanged += _ => Dispatcher.UIThread.Post(OnConfigChanged);
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

        // Build the Language submenu ONCE. Pre-create a NativeMenuItem for every
        // supported language and stash them in _allLangItems. The submenu itself
        // (the same NativeMenu instance) is mutated in place when the enabled
        // list changes — we never reassign _languageRoot.Menu, since reassignment
        // doesn't reliably propagate to AppKit's NSMenu cache.
        BuildLanguageMenuPersistent();
        _languageRoot = new NativeMenuItem("Language") { Menu = _languageSubmenu };
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

    private void BuildLanguageMenuPersistent()
    {
        _languageSubmenu = new NativeMenu();

        _autoLangItem = new NativeMenuItem(FormatHeader("Auto-detect", _host.Config.AutoLanguage));
        _autoLangItem.Click += (_, _) =>
        {
            _host.UpdateConfig(_host.Config.WithAutoLanguage(true));
        };

        _afterAutoSeparator = new NativeMenuItemSeparator();
        _beforeSettingsSeparator = new NativeMenuItemSeparator();

        _openLangSettingsItem = new NativeMenuItem("Open settings…");
        _openLangSettingsItem.Click += (_, _) => ShowMain(MainTab.Languages);

        // Pre-create persistent items for every supported language. They are
        // not added to the submenu yet — PopulateLanguageItems decides which
        // ones go in based on _host.Config.Languages.
        foreach (var info in Languages.Common)
        {
            var label = $"{info.NativeName}  ({info.DisplayName})";
            var item = new NativeMenuItem(FormatHeader(label, false));
            var capturedCode = info.Code;
            item.Click += (_, _) =>
            {
                _host.UpdateConfig(_host.Config.WithLanguage(capturedCode));
            };
            _allLangItems[info.Code] = item;
        }

        PopulateLanguageItems();
        RefreshLanguageMarks();
    }

    private void PopulateLanguageItems()
    {
        if (_languageSubmenu is null) return;
        _languageSubmenu.Items.Clear();
        _languageSubmenu.Items.Add(_autoLangItem!);
        _languageSubmenu.Items.Add(_afterAutoSeparator!);
        foreach (var code in _host.Config.Languages)
        {
            if (_allLangItems.TryGetValue(code, out var item))
            {
                _languageSubmenu.Items.Add(item);
            }
        }
        _languageSubmenu.Items.Add(_beforeSettingsSeparator!);
        _languageSubmenu.Items.Add(_openLangSettingsItem!);
    }

    private void RefreshLanguageMarks()
    {
        if (_autoLangItem is not null)
        {
            _autoLangItem.Header = FormatHeader("Auto-detect", _host.Config.AutoLanguage);
        }
        foreach (var (code, item) in _allLangItems)
        {
            var info = Languages.Find(code);
            var label = info is null ? code : $"{info.NativeName}  ({info.DisplayName})";
            var checkedNow = !_host.Config.AutoLanguage
                && string.Equals(code, _host.Config.ActiveLanguage, StringComparison.OrdinalIgnoreCase);
            item.Header = FormatHeader(label, checkedNow);
        }
    }

    private void OnConfigChanged()
    {
        // Rebuild membership (enabled-list changed) and refresh check marks
        // (active or auto-detect changed). PopulateLanguageItems mutates the
        // SAME NativeMenu instance that's already attached to NSMenu, which
        // AppKit handles correctly via removeAllItems + addItem.
        PopulateLanguageItems();
        RefreshLanguageMarks();
    }

    private static string FormatHeader(string label, bool isActive) =>
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
