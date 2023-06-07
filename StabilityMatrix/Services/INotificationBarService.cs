using Wpf.Ui.Contracts;

namespace StabilityMatrix.Services;

public interface INotificationBarService : ISnackbarService
{
    public void ShowStartupNotifications();
}