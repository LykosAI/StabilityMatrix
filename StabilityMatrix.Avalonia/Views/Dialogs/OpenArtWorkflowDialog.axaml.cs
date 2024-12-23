using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<OpenArtWorkflowDialog>]
public partial class OpenArtWorkflowDialog : UserControlBase
{
    public OpenArtWorkflowDialog()
    {
        InitializeComponent();
    }
}
