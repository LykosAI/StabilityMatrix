using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<LaunchOptionsDialog>]
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
