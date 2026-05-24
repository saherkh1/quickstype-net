using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuickSType.UI.Composition;
using QuickSType.UI.ViewModels;

namespace QuickSType.UI.Views;

public partial class LanguageModelFlyoutWindow : Window
{
    // Designer-only ctor — required for Avalonia compiled XAML loader.
    public LanguageModelFlyoutWindow()
    {
        InitializeComponent();
    }

    public LanguageModelFlyoutWindow(string langCode, AppHost host) : this()
    {
        DataContext = new LanguageModelFlyoutViewModel(langCode, host);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
