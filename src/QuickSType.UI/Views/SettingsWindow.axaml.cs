using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuickSType.UI.Composition;

namespace QuickSType.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(AppHost host) : this()
    {
        Title = "QuickSType — Settings";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void FocusLanguages()
    {
        var section = this.FindControl<StackPanel>("LanguagesSection");
        section?.Focus();
    }
}
