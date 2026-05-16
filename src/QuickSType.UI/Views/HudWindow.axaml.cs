using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core;
using QuickSType.UI.ViewModels;

namespace QuickSType.UI.Views;

public partial class HudWindow : Window
{
    private readonly ILogger _log;
    private DispatcherTimer? _blinkTimer;
    private bool _blinkState;

    public HudWindow()
    {
        _log = NullLogger.Instance;
        InitializeComponent();
    }

    public HudWindow(ILogger<HudWindow>? log = null) : this()
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyClickThrough();
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TryGetPlatformHandle is safe on desktop Avalonia targets; tested path")]
    private void ApplyClickThrough()
    {
        try
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                _log.LogWarning("HudWindow: TryGetPlatformHandle returned null — click-through not applied");
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                ApplyClickThroughMac(handle);
            }
            else if (OperatingSystem.IsWindows())
            {
                ApplyClickThroughWindows(handle);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "HudWindow.ApplyClickThrough failed");
        }
    }

    private static void ApplyClickThroughMac(IntPtr handle)
    {
        // NSWindow.setIgnoresMouseEvents: YES
        var sel = sel_getUid("setIgnoresMouseEvents:");
        objc_msgSend_bool(handle, sel, true);
    }

    private static void ApplyClickThroughWindows(IntPtr hwnd)
    {
        const int GwlExStyle = -20;
        const int WsExLayered = 0x00080000;
        const int WsExTransparent = 0x00000020;
        const int WsExNoActivate = 0x08000000;

        var style = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, style | WsExLayered | WsExTransparent | WsExNoActivate);
    }

    /// <summary>
    /// Position the HUD relative to the tray icon rectangle.
    /// Call this on every Show() so the HUD follows tray icon position changes.
    /// </summary>
    public void PositionNearTray(Rectangle trayRect)
    {
        var isWindows = OperatingSystem.IsWindows();
        int hudW = (int)Width;
        int hudH = (int)Height;
        int x, y;

        if (trayRect.IsEmpty)
        {
            // Fallback: top-right of primary screen, 16px from edges
            var screen = Screens.Primary;
            if (screen is not null)
            {
                var area = screen.WorkingArea;
                x = area.X + area.Width - hudW - 16;
                y = area.Y + 16;
            }
            else
            {
                x = 1920 - hudW - 16;
                y = 16;
            }
        }
        else
        {
            x = trayRect.Left + (trayRect.Width - hudW) / 2;
            y = isWindows
                ? trayRect.Top - hudH - 8   // Windows: rise above tray
                : trayRect.Bottom + 8;       // macOS: drop below tray
        }

        Position = new PixelPoint(x, y);
    }

    public void OnStateChanged(DictationState state)
    {
        if (DataContext is not HudViewModel vm) return;
        vm.OnStateChanged(state);

        // Manage blink timer for Recording state
        if (state == DictationState.Recording)
        {
            StartBlink();
        }
        else
        {
            StopBlink();
        }
    }

    private void StartBlink()
    {
        if (_blinkTimer is { IsEnabled: true }) return;
        _blinkState = true;
        _blinkTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += OnBlinkTick;
        _blinkTimer.Start();
    }

    private void StopBlink()
    {
        if (_blinkTimer is null) return;
        _blinkTimer.Stop();
        _blinkTimer.Tick -= OnBlinkTick;
        var dot = this.FindControl<Avalonia.Controls.Shapes.Ellipse>("RecDot");
        if (dot is not null) dot.Opacity = 1.0;
    }

    private void OnBlinkTick(object? sender, EventArgs e)
    {
        _blinkState = !_blinkState;
        var dot = this.FindControl<Avalonia.Controls.Shapes.Ellipse>("RecDot");
        if (dot is not null) dot.Opacity = _blinkState ? 1.0 : 0.2;
    }

    // macOS P/Invoke — sel_getUid with ANSI marshalling to avoid IL3050
    [LibraryImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_getUid")]
    private static partial IntPtr sel_getUid([MarshalAs(UnmanagedType.LPStr)] string name);

    [LibraryImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static partial void objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

    // Windows P/Invoke
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
