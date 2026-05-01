using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace QuickSType.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
