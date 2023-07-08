using Avalonia;
using Avalonia.Controls.Notifications;

namespace StabilityMatrix.Avalonia.Services;

public interface INotificationService
{
    public void Initialize(
        Visual? visual,
        NotificationPosition position = NotificationPosition.BottomRight,
        int maxItems = 3);

    public void Show(INotification notification);
}
