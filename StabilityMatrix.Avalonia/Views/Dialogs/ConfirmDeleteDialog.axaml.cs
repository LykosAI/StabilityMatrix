using Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Transient]
public partial class ConfirmDeleteDialog : UserControl
{
    public ConfirmDeleteDialog()
    {
        InitializeComponent();
    }
}
