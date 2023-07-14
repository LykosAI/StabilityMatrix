using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class LaunchOptionsDialog : UserControl
{
    public LaunchOptionsDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
