using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<OpenModelDbModelDetailsDialog>]
public partial class OpenModelDbModelDetailsDialog : UserControlBase
{
    public OpenModelDbModelDetailsDialog()
    {
        InitializeComponent();
    }
}
