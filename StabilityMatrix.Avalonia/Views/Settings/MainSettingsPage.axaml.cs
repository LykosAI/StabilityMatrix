using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterSingleton<MainSettingsPage>]
public partial class MainSettingsPage : UserControlBase
{
    public MainSettingsPage()
    {
        InitializeComponent();
    }
}
