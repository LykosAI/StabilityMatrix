using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class OneClickInstallDialog : UserControl
{
    public OneClickInstallDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}