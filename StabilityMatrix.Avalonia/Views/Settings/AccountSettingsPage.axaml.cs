using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterSingleton<AccountSettingsPage>]
public partial class AccountSettingsPage : UserControlBase
{
    public AccountSettingsPage()
    {
        InitializeComponent();
    }
}
