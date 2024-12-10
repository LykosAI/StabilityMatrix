using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterSingleton<NotificationSettingsPage>]
public partial class NotificationSettingsPage : UserControlBase
{
    public NotificationSettingsPage()
    {
        InitializeComponent();
    }
}
