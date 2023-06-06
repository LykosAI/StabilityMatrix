using System.Threading.Tasks;
using Wpf.Ui.Contracts;
using Wpf.Ui.Services;

namespace StabilityMatrix.Services;

public interface INotificationBarService : ISnackbarService
{
    public void ShowStartupNotifications();
}

public class NotificationBarService : SnackbarService, INotificationBarService
{
    public void ShowStartupNotifications()
    {
    }
}
