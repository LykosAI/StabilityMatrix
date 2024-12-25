using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<OAuthConnectDialog>]
public partial class OAuthConnectDialog : UserControlBase
{
    public OAuthConnectDialog()
    {
        InitializeComponent();
    }
}
