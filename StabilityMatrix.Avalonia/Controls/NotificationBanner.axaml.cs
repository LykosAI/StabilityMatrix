using Avalonia.Controls;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[RegisterTransient<NotificationBanner>]
public partial class NotificationBanner : UserControlBase
{
    public NotificationBanner()
    {
        InitializeComponent();
    }
}
