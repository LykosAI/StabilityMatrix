using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<UpdateDialog>]
public partial class UpdateDialog : UserControl
{
    public UpdateDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
