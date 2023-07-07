using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.Views;

public partial class LaunchPageView : UserControl
{
    public LaunchPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
