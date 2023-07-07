using AsyncAwaitBestPractices;
using StabilityMatrix.Core.Services;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.IconElements;
using SnackbarService = Wpf.Ui.Services.SnackbarService;

namespace StabilityMatrix.Services;

public class NotificationBarService : SnackbarService, INotificationBarService
{
    private readonly ISettingsManager settingsManager;

    public NotificationBarService(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
    }
    
    public void ShowStartupNotifications()
    {
        if (settingsManager.Settings.HasSeenWelcomeNotification) 
            return;
        
        Timeout = 10000;
        var linkIcon = new SymbolIcon(SymbolRegular.Link24);
        var snackbar = ShowAsync(
            "Welcome to StabilityMatrix!",
            "You can join our Discord server for support and feedback.", linkIcon, ControlAppearance.Info);
        snackbar.SafeFireAndForget();
        
        settingsManager.Transaction(s => s.HasSeenWelcomeNotification = true);
    }
}
