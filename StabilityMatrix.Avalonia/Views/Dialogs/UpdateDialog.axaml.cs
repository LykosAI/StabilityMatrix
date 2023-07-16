using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

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