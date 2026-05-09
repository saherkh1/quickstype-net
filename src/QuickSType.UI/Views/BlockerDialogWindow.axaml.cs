using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Platform;

namespace QuickSType.UI.Views;

public partial class BlockerDialogWindow : Window
{
    private readonly ILogger _log;
    private readonly Action? _onOpenSettings;
    private bool _openingSettings;

    // Designer-only ctor — required for Avalonia compiled XAML loader.
    public BlockerDialogWindow()
    {
        _log = NullLogger.Instance;
        InitializeComponent();
    }

    public BlockerDialogWindow(SystemSpecs specs, Action onOpenSettings, ILogger<BlockerDialogWindow>? log = null) : this()
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _onOpenSettings = onOpenSettings;

        // S1 body copy with token substitution. UI-SPEC S1: one decimal, "unknown" if undetected.
        var ramStr = specs.TotalRamGb > 0 ? $"{Math.Round(specs.TotalRamGb, 1):F1}" : "unknown";
        var diskStr = specs.FreeDiskGb > 0 ? $"{Math.Round(specs.FreeDiskGb, 1):F1}" : "unknown";

        var bodyTb = this.FindControl<TextBlock>("BodyText");
        if (bodyTb is not null)
        {
            bodyTb.Text =
                $"QuickSType needs at least 2 GB of free RAM and 1 GB of free disk to run the smallest model. " +
                $"Your system reports {ramStr} GB RAM and {diskStr} GB free disk.";
        }

        var openBtn = this.FindControl<Button>("OpenSettingsButton");
        if (openBtn is not null) openBtn.Click += OnOpenSettingsClick;
        var quitBtn = this.FindControl<Button>("QuitButton");
        if (quitBtn is not null) quitBtn.Click += OnQuitClick;

        KeyDown += OnKeyDown;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnOpenSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            _openingSettings = true;
            _log.LogInformation("Blocker resolved by user: OpenSettings");
            _onOpenSettings?.Invoke();
            Close();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OpenSettings handler threw");
            Environment.Exit(0);
        }
    }

    private void OnQuitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _log.LogInformation("Blocker resolved by user: Quit");
        Environment.Exit(0);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Esc → same as Quit (D-06; UI-SPEC S1 keyboard map).
        if (e.Key == Key.Escape)
        {
            _log.LogInformation("Blocker resolved by user: Quit (Esc)");
            Environment.Exit(0);
        }
    }

    // Pitfall 3: titlebar X / programmatic close → treat as Quit unless we're closing because the user clicked Open Settings.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_openingSettings)
        {
            _log.LogInformation("Blocker resolved by user: Quit (close gesture)");
            base.OnClosing(e);
            Environment.Exit(0);
            return;
        }
        base.OnClosing(e);
    }
}
