using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<InferenceConnectionHelpDialog>]
public partial class InferenceConnectionHelpDialog : UserControl
{
    public InferenceConnectionHelpDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
