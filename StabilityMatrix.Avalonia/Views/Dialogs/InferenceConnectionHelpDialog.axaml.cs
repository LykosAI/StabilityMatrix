using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<InferenceConnectionHelpDialog>]
public partial class InferenceConnectionHelpDialog : UserControlBase
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
