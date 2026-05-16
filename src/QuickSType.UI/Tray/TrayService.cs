using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using QuickSType.Core;
using QuickSType.UI.Composition;
using QuickSType.UI.Updates;
using QuickSType.UI.Views;

namespace QuickSType.UI.Tray;

public sealed class TrayService
{
    private readonly AppHost _host;
    private readonly ILogger _log;
    private TrayIcon? _trayIcon;
    private NativeMenu? _menu;
    private NativeMenuItem? _stateItem;
    private NativeMenuItem? _modeItem;
    private NativeMenuItem? _updateItem;
    private NativeMenuItem? _languageRoot;
    private NativeMenu? _languageSubmenu;
    private NativeMenuItem? _autoLangItem;
    private NativeMenuItem? _openLangSettingsItem;
    private NativeMenuItemSeparator? _afterAutoSeparator;
    private NativeMenuItemSeparator? _beforeSettingsSeparator;
    private readonly Dictionary<string, NativeMenuItem> _allLangItems = new(StringComparer.OrdinalIgnoreCase);
    private NativeMenuItem? _detectedLayoutItem;
    private NativeMenuItemSeparator? _detectedLayoutSeparator;
    private MainWindow? _mainWindow;
    private UpdateCheckResult? _lastUpdateResult;
    private bool _isUpdateBusy;
    private DateTimeOffset? _lastUpdateChecked;

    public TrayService(AppHost host)
    {
        _host = host;
        _log = host.LoggerFactory.CreateLogger<TrayService>();
        _host.Engine.StateChanged += OnStateChanged;
        _host.ConfigChanged += _ => Dispatcher.UIThread.Post(OnConfigChanged);
        _host.KeyboardLayout.LayoutChanged += layout =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_detectedLayoutItem is not null)
                    _detectedLayoutItem.Header = FormatDetectedHeader(layout.DisplayName);
            });
        };
        _host.Engine.ModeChanged += effective =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_modeItem is not null)
                    _modeItem.Header = FormatModeHeader(effective, IsAutoDegraded(effective));
            });
        };
        _host.ConfigChanged += _ => Dispatcher.UIThread.Post(() =>
        {
            if (_modeItem is not null)
                _modeItem.Header = FormatModeHeader(_host.Engine.EffectiveMode, IsAutoDegraded(_host.Engine.EffectiveMode));
        });
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
        _modeItem = new NativeMenuItem(FormatModeHeader(_host.Engine.EffectiveMode, IsAutoDegraded(_host.Engine.EffectiveMode))) { IsEnabled = false };
        menu.Add(_modeItem);
        menu.Add(new NativeMenuItemSeparator());

        var settings = new NativeMenuItem("Open QuickSType…");
        settings.Click += (_, _) => ShowMain(MainTab.General);
        menu.Add(settings);

        _updateItem = new NativeMenuItem();
        _updateItem.Click += async (_, _) => await OnUpdateClickAsync();
        RefreshUpdateItem();
        menu.Add(_updateItem);

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

        _detectedLayoutItem = new NativeMenuItem(FormatDetectedHeader(_host.KeyboardLayout.CurrentLayout.DisplayName)) { IsEnabled = false };
        _detectedLayoutSeparator = new NativeMenuItemSeparator();

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
        _languageSubmenu.Items.Add(_detectedLayoutItem!);
        _languageSubmenu.Items.Add(_detectedLayoutSeparator!);
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

    internal static string FormatDetectedHeader(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) ? $"Detected: {displayName}" : "Detected: —";

    internal static string FormatModeHeader(string effectiveMode, bool isAutoDegraded)
    {
        if (effectiveMode == "streaming") return "Mode: Streaming";
        if (effectiveMode == "commit-on-pause" && isAutoDegraded) return "Mode: Commit-on-pause (hardware)";
        return "Mode: Commit-on-pause";
    }

    private bool IsAutoDegraded(string effectiveMode) =>
        effectiveMode == "commit-on-pause" && _host.Config.StreamingMode != "commit-on-pause";

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
                    DictationState.Streaming => "● Streaming…",
                    _ => "● Idle",
                };
            }
            if (_trayIcon is not null)
            {
                _trayIcon.Icon = LoadIcon(state switch
                {
                    DictationState.Recording => "recording",
                    DictationState.Processing => "processing",
                    DictationState.Streaming => "recording",
                    _ => "idle",
                });
            }
            RefreshUpdateItem();
        });
    }

    private async Task OnUpdateClickAsync()
    {
        if (_isUpdateBusy || _updateItem is null) return;

        var currentStatus = _lastUpdateResult?.Status ?? _host.UpdateService.GetCurrentStatus(_host.Config);
        if (_lastUpdateResult is not null
            && currentStatus.Kind is UpdateStatusKind.UpdateAvailable or UpdateStatusKind.UpdateReadyToRestart)
        {
            if (_host.Engine.State != DictationState.Idle) return;
            try
            {
                _host.UpdateService.ApplyUpdatesAndRestart(_lastUpdateResult!);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Apply update from tray failed");
            }
            return;
        }

        _isUpdateBusy = true;
        RefreshUpdateItem();
        try
        {
            var result = await _host.UpdateService.CheckForUpdatesAsync(_host.Config);
            _lastUpdateChecked = DateTimeOffset.Now;
            if (result.Status.Kind == UpdateStatusKind.UpdateAvailable)
            {
                await _host.UpdateService.DownloadUpdatesAsync(result);
                result = result with
                {
                    Status = result.Status with
                    {
                        Kind = UpdateStatusKind.UpdateReadyToRestart,
                        Message = result.TargetVersion is null
                            ? "Update is ready to install."
                            : $"Update {result.TargetVersion} is ready to install.",
                    },
                };
            }
            _lastUpdateResult = result;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Update check from tray failed");
        }
        finally
        {
            _isUpdateBusy = false;
            RefreshUpdateItem();
        }
    }

    private void RefreshUpdateItem()
    {
        if (_updateItem is null) return;

        var status = _lastUpdateResult?.Status ?? _host.UpdateService.GetCurrentStatus(_host.Config);
        var ui = UpdatePresentation.FromStatus(status, _lastUpdateChecked, _isUpdateBusy, _host.Engine.State);
        _updateItem.Header = ui.TrayHeader;
        _updateItem.IsEnabled = ui.IsActionEnabled;
    }

    private void ShowMain(MainTab tab)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow is null)
            {
                _mainWindow = new MainWindow(_host, _host.LoggerFactory.CreateLogger<MainWindow>());
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

    private WindowIcon? LoadIcon(string name)
    {
        try
        {
            var uri = new Uri($"avares://QuickSType/Assets/tray-{name}.png");
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to load tray icon {Name}", name);
            return null;
        }
    }
}
