using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<LykosLoginDialog>]
public partial class LykosLoginDialog : UserControlBase
{
    public LykosLoginDialog()
    {
        InitializeComponent();
    }
}
