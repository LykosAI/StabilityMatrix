using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<OAuthLoginDialog>]
public partial class OAuthLoginDialog : UserControlBase
{
    public OAuthLoginDialog()
    {
        InitializeComponent();
    }
}
