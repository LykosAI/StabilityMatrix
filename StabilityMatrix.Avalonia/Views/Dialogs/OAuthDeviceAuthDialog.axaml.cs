using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<OAuthDeviceAuthDialog>]
public partial class OAuthDeviceAuthDialog : UserControlBase
{
    public OAuthDeviceAuthDialog()
    {
        InitializeComponent();
    }
}
