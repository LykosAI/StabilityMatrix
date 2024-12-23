using Avalonia.Controls;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<LykosLoginDialog>]
public partial class LykosLoginDialog : UserControl
{
    public LykosLoginDialog()
    {
        InitializeComponent();
    }
}
