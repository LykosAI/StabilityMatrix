using Avalonia;
using Avalonia.Controls.Notifications;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockNotificationService : INotificationService
{
    public void Initialize(Visual? visual, 
        NotificationPosition position = NotificationPosition.BottomRight, int maxItems = 3)
    {
    }

    public void Show(INotification notification)
    {
    }
}
