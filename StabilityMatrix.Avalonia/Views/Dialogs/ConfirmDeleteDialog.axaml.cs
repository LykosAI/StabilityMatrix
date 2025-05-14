using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ConfirmDeleteDialog>]
public partial class ConfirmDeleteDialog : UserControlBase
{
    public ConfirmDeleteDialog()
    {
        InitializeComponent();
    }
}
