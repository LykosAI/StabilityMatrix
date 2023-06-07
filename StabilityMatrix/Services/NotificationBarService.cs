using AsyncAwaitBestPractices;
using Wpf.Ui.Common;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.IconElements;
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
        Timeout = 10000;
        var linkIcon = new SymbolIcon(SymbolRegular.Link24);
        var snackbar = ShowAsync(
            "Welcome to StabilityMatrix!",
            "You can join our Discord server for support and feedback.", linkIcon, ControlAppearance.Info);
        snackbar.SafeFireAndForget();
    }
}
