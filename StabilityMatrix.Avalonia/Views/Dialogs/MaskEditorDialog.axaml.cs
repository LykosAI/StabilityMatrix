using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<MaskEditorDialog>]
public partial class MaskEditorDialog : UserControlBase
{
    public MaskEditorDialog()
    {
        InitializeComponent();
    }
}
