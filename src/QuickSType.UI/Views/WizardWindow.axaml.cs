using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using QuickSType.Core.Transcribe;
using QuickSType.UI.Composition;

namespace QuickSType.UI.Views;

public partial class WizardWindow : Window
{
    private readonly AppHost _host;
    private int _step;

    public WizardWindow()
    {
        _host = null!;
        InitializeComponent();
    }

    public WizardWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        Render();

        var back = this.FindControl<Button>("BackButton");
        var next = this.FindControl<Button>("NextButton");
        back!.Click += (_, _) => { if (_step > 0) { _step--; Render(); } };
        next!.Click += (_, _) =>
        {
            if (_step < 4) { _step++; Render(); }
            else { Close(); }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Render()
    {
        var host = this.FindControl<StackPanel>("StepHost");
        if (host is null) return;
        host.Children.Clear();

        switch (_step)
        {
            case 0: RenderWelcome(host); break;
            case 1: RenderMicrophone(host); break;
            case 2: RenderHotkeyPermission(host); break;
            case 3: RenderModelDownload(host); break;
            case 4: RenderDone(host); break;
        }

        var back = this.FindControl<Button>("BackButton");
        var next = this.FindControl<Button>("NextButton");
        if (back is not null) back.IsEnabled = _step > 0;
        if (next is not null) next.Content = _step == 4 ? "Done" : "Next";
    }

    private void RenderWelcome(StackPanel host)
    {
        host.Children.Add(new TextBlock { Text = "Welcome to QuickSType", FontSize = 22, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        host.Children.Add(new TextBlock { Text = "Push-to-talk voice dictation, 100% local. Hold a key, speak, release — text appears at your cursor.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        host.Children.Add(new TextBlock { Text = "This wizard will set up the microphone, accessibility permissions, and download a transcription model.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 });
    }

    private void RenderMicrophone(StackPanel host)
    {
        host.Children.Add(new TextBlock { Text = "Microphone access", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        host.Children.Add(new TextBlock { Text = "QuickSType needs microphone access. The system prompt should appear automatically the first time you record. If it does not, open System Settings and grant access.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var open = new Button { Content = "Open microphone settings", HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += (_, _) => _host.Permissions.OpenMicrophoneSettings();
        host.Children.Add(open);
    }

    private void RenderHotkeyPermission(StackPanel host)
    {
        host.Children.Add(new TextBlock { Text = "Global hotkey permission", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        host.Children.Add(new TextBlock { Text = "QuickSType listens for a global key press to start recording. On macOS, you must grant Accessibility and Input Monitoring permissions, and then quit and relaunch the app.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var b1 = new Button { Content = "Open Accessibility settings" };
        b1.Click += (_, _) => _host.Permissions.OpenAccessibilitySettings();
        var b2 = new Button { Content = "Open Input Monitoring settings" };
        b2.Click += (_, _) => _host.Permissions.OpenInputMonitoringSettings();
        sp.Children.Add(b1); sp.Children.Add(b2);
        host.Children.Add(sp);
    }

    private void RenderModelDownload(StackPanel host)
    {
        host.Children.Add(new TextBlock { Text = "Download a Whisper model", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        var picker = new ComboBox { ItemsSource = ModelCatalog.All, SelectedItem = ModelCatalog.Default, HorizontalAlignment = HorizontalAlignment.Stretch };
        picker.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ModelInfo>((m, _) =>
            new TextBlock { Text = $"{m.DisplayName} — {m.Description}" }, supportsRecycling: true);
        host.Children.Add(picker);
        var bar = new ProgressBar { Maximum = 100, Height = 8 };
        var status = new TextBlock { Text = "Pick a model and click Download.", Opacity = 0.7, FontSize = 12 };
        var dl = new Button { Content = "Download" };
        dl.Click += async (_, _) =>
        {
            if (picker.SelectedItem is not ModelInfo m) return;
            dl.IsEnabled = false;
            try
            {
                var prog = new Progress<ModelDownloader.Progress>(p =>
                {
                    bar.Value = p.Percent;
                    status.Text = $"{p.DownloadedBytes / 1_000_000.0:F1} / {p.TotalBytes / 1_000_000.0:F1} MB ({p.BytesPerSecond / 1_000_000.0:F1} MB/s)";
                });
                await _host.ModelDownloader.DownloadAsync(m, prog);
                status.Text = "Downloaded.";
                _host.UpdateConfig(_host.Config with { Model = m.Id });
            }
            catch (Exception ex)
            {
                status.Text = $"Error: {ex.Message}";
            }
            finally { dl.IsEnabled = true; }
        };
        host.Children.Add(dl);
        host.Children.Add(bar);
        host.Children.Add(status);
    }

    private void RenderDone(StackPanel host)
    {
        host.Children.Add(new TextBlock { Text = "All set", FontSize = 22, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        host.Children.Add(new TextBlock { Text = "Hold the hotkey to record. Release to paste. Use the menu-bar / tray icon to switch language or open settings.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        host.Children.Add(new TextBlock { Text = "If the hotkey does not work on macOS, fully quit QuickSType and relaunch — Accessibility permissions only take effect after a restart of the app.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 });
    }
}
