using Avalonia.Controls;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ConfirmDeleteDialog>]
public partial class ConfirmDeleteDialog : UserControl
{
    public ConfirmDeleteDialog()
    {
        InitializeComponent();
    }
}
