using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickSType.Core;
using QuickSType.Core.Audio;
using QuickSType.Core.Config;
using QuickSType.Core.History;
using QuickSType.Core.Hotkey;
using QuickSType.Core.Paste;
using QuickSType.Core.Platform;
using QuickSType.Core.Transcribe;
using System.Drawing;

namespace QuickSType.UI.Composition;

public sealed class AppHost : IDisposable
{
    public ConfigStoreWithExists ConfigStore { get; }
    public AppConfig Config { get; private set; }
    public IAudioCapture Audio { get; }
    public Transcriber Transcriber { get; }
    public IPasteService Paste { get; }
    public INotificationService Notifications { get; }
    public IPermissionService Permissions { get; }
    public IAutoLaunchService AutoLaunch { get; }
    public HotkeyService Hotkey { get; }
    public DictationEngine Engine { get; }
    public ILoggerFactory LoggerFactory { get; }
    public ModelDownloader ModelDownloader { get; }
    public ISystemSpecsService SystemSpecsService { get; }
    public SystemSpecs SystemSpecs { get; }
    public IHistoryService History { get; }
    public IKeyboardLayoutService KeyboardLayout { get; }
    public ITrayPositionService TrayPosition { get; }
    public ISystemThemeService SystemTheme { get; }

    private AppHost(
        ConfigStoreWithExists configStore,
        AppConfig config,
        IAudioCapture audio,
        Transcriber transcriber,
        IPasteService paste,
        INotificationService notifications,
        IPermissionService permissions,
        IAutoLaunchService autoLaunch,
        HotkeyService hotkey,
        DictationEngine engine,
        ILoggerFactory lf,
        ModelDownloader downloader,
        ISystemSpecsService specsService,
        SystemSpecs systemSpecs,
        IHistoryService history,
        IKeyboardLayoutService keyboardLayout,
        ITrayPositionService trayPosition,
        ISystemThemeService systemTheme)
    {
        ConfigStore = configStore;
        Config = config;
        Audio = audio;
        Transcriber = transcriber;
        Paste = paste;
        Notifications = notifications;
        Permissions = permissions;
        AutoLaunch = autoLaunch;
        Hotkey = hotkey;
        Engine = engine;
        LoggerFactory = lf;
        ModelDownloader = downloader;
        SystemSpecsService = specsService;
        SystemSpecs = systemSpecs;
        History = history;
        KeyboardLayout = keyboardLayout;
        TrayPosition = trayPosition;
        SystemTheme = systemTheme;
    }

    public static AppHost Create()
    {
        var lf = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var configStore = new ConfigStoreWithExists(lf.CreateLogger<ConfigStore>());
        var config = configStore.Load();

        // CONFIG-02 / CONFIG-03 / D-08: detect hardware specs ONCE, BEFORE any WhisperFactory is constructed.
        // (Transcriber's constructor does not create a WhisperFactory; EnsureLoaded() does, on first dictation.
        //  Pattern 4 / Pitfall 2: RuntimeOptions.RuntimeLibraryOrder is global static — the CUDA probe inside
        //  SystemSpecsService.Detect() saves and restores it, so subsequent WhisperFactory.FromPath calls
        //  see the original library order.)
        var specsService = new SystemSpecsService(lf.CreateLogger<SystemSpecsService>());
        var systemSpecs = specsService.Detect();

        // CONFIG-05: history writer (path under Application Support / LocalAppData; same dir family as ConfigStore).
        var historyService = new HistoryService(lf.CreateLogger<HistoryService>());

        var audio = new PortAudioCapture(lf.CreateLogger<PortAudioCapture>());
        var transcriber = new Transcriber(lf.CreateLogger<Transcriber>());
        var downloader = new ModelDownloader(lf.CreateLogger<ModelDownloader>());

        IPasteService paste;
        INotificationService notify;
        IPermissionService permissions;
        IAutoLaunchService autoLaunch;
        IKeyboardLayoutService keyboardLayout;
        ITrayPositionService trayPosition;
        ISystemThemeService systemTheme;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            paste = new Platform.Mac.MacPasteService(lf.CreateLogger<Platform.Mac.MacPasteService>());
            notify = new Platform.Mac.MacNotifications(lf.CreateLogger<Platform.Mac.MacNotifications>());
            permissions = new Platform.Mac.MacPermissions(lf.CreateLogger<Platform.Mac.MacPermissions>());
            autoLaunch = new Platform.Mac.MacAutoLaunch(lf.CreateLogger<Platform.Mac.MacAutoLaunch>());
            keyboardLayout = new Platform.Mac.MacKeyboardLayoutService(lf.CreateLogger<Platform.Mac.MacKeyboardLayoutService>());
            trayPosition = new Platform.Mac.MacTrayPositionService(lf.CreateLogger<Platform.Mac.MacTrayPositionService>());
            systemTheme = new Platform.Mac.MacSystemThemeService(lf.CreateLogger<Platform.Mac.MacSystemThemeService>());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            paste = new Platform.Windows.WindowsPasteService(lf.CreateLogger<Platform.Windows.WindowsPasteService>());
            notify = new Platform.Windows.WindowsNotifications(lf.CreateLogger<Platform.Windows.WindowsNotifications>());
            permissions = new Platform.Windows.WindowsPermissions(lf.CreateLogger<Platform.Windows.WindowsPermissions>());
            autoLaunch = new Platform.Windows.WindowsAutoLaunch();
            keyboardLayout = new Platform.Windows.WindowsKeyboardLayoutService(lf.CreateLogger<Platform.Windows.WindowsKeyboardLayoutService>());
            trayPosition = new Platform.Windows.WindowsTrayPositionService(lf.CreateLogger<Platform.Windows.WindowsTrayPositionService>());
            systemTheme = new Platform.Windows.WindowsSystemThemeService(lf.CreateLogger<Platform.Windows.WindowsSystemThemeService>());
        }
        else
        {
            paste = new NoopPasteService();
            notify = new NoopNotifications();
            permissions = new NoopPermissions();
            autoLaunch = new NoopAutoLaunch();
            keyboardLayout = new NoopKeyboardLayoutService();
            trayPosition = new NoopTrayPositionService();
            systemTheme = new NoopSystemThemeService();
        }

        var hotkey = new HotkeyService(config.Hotkey, lf.CreateLogger<HotkeyService>());
        var engine = new DictationEngine(
            audio, transcriber, paste, notify, configStore, config,
            log: lf.CreateLogger<DictationEngine>(),
            history: historyService);

        hotkey.Pressed += engine.OnHotkeyPressed;
        hotkey.Released += engine.OnHotkeyReleased;

        return new AppHost(
            configStore, config, audio, transcriber, paste, notify, permissions, autoLaunch,
            hotkey, engine, lf, downloader,
            specsService, systemSpecs, historyService,
            keyboardLayout, trayPosition, systemTheme);
    }

    public event Action<AppConfig>? ConfigChanged;

    public Task StartAsync()
    {
        _ = Hotkey.RunAsync();
        return Task.CompletedTask;
    }

    public void UpdateConfig(AppConfig cfg)
    {
        Config = cfg;
        Engine.UpdateConfig(cfg);
        Hotkey.SetHotkey(cfg.Hotkey);
        try { ConfigChanged?.Invoke(cfg); }
        catch (Exception ex) { LoggerFactory.CreateLogger<AppHost>().LogError(ex, "ConfigChanged handler threw"); }
    }

    public void Dispose()
    {
        Engine.Dispose();
        Hotkey.Dispose();
        (KeyboardLayout as IDisposable)?.Dispose();
        (TrayPosition as IDisposable)?.Dispose();
        (SystemTheme as IDisposable)?.Dispose();
        LoggerFactory.Dispose();
    }
}

public sealed class ConfigStoreWithExists : ConfigStore
{
    public ConfigStoreWithExists(ILogger<ConfigStore>? log = null, string? overridePath = null)
        : base(log, overridePath) { }

    public bool PathExists() => File.Exists(FilePath);
}

internal sealed class NoopPasteService : IPasteService
{
    public Task PasteAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NoopNotifications : INotificationService
{
    public void Notify(string title, string message, string? subtitle = null) { }
    public Task PlayStartAsync() => Task.CompletedTask;
    public Task PlayStopAsync() => Task.CompletedTask;
}

internal sealed class NoopPermissions : IPermissionService
{
    public bool HasMicrophoneAccess() => true;
    public bool HasInputMonitoringAccess() => true;
    public bool HasAccessibilityAccess() => true;
    public void OpenMicrophoneSettings() { }
    public void OpenInputMonitoringSettings() { }
    public void OpenAccessibilitySettings() { }
    public void RequestAccessibilityIfNeeded() { }
}

internal sealed class NoopAutoLaunch : IAutoLaunchService
{
    public bool IsEnabled() => false;
    public void SetEnabled(bool enabled, string? executablePath = null) { }
}

internal sealed class NoopKeyboardLayoutService : IKeyboardLayoutService
{
#pragma warning disable CS0067
    public event Action<InputLayout>? LayoutChanged;
#pragma warning restore CS0067
    public InputLayout CurrentLayout => new("—", "");
}

internal sealed class NoopTrayPositionService : ITrayPositionService
{
    public Rectangle GetTrayRect() => Rectangle.Empty;
}

internal sealed class NoopSystemThemeService : ISystemThemeService
{
#pragma warning disable CS0067
    public event Action<AppTheme>? Changed;
#pragma warning restore CS0067
    public AppTheme Current => new(false, "#007AFF");
}
