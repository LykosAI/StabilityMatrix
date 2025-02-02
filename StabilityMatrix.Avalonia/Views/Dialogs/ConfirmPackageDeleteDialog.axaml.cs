using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ConfirmPackageDeleteDialog>]
public partial class ConfirmPackageDeleteDialog : UserControlBase
{
    public ConfirmPackageDeleteDialog()
    {
        InitializeComponent();
    }
}
