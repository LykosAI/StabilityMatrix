using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using DesktopNotifications;
using NLog;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Extensions;

public static class NotificationServiceExtensions
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static void OnPackageInstallCompleted(
        this INotificationService notificationService,
        IPackageModificationRunner runner
    )
    {
        OnPackageInstallCompletedAsync(notificationService, runner)
            .SafeFireAndForget(ex => Logger.Error(ex, "Error Showing Notification"));
    }

    private static async Task OnPackageInstallCompletedAsync(
        this INotificationService notificationService,
        IPackageModificationRunner runner
    )
    {
        if (runner.Failed)
        {
            Logger.Error(runner.Exception, "Error Installing Package");

            await notificationService.ShowAsync(
                NotificationKey.Package_Install_Failed,
                new Notification
                {
                    Title = runner.ModificationFailedTitle,
                    Body = runner.ModificationFailedMessage
                }
            );
        }
        else
        {
            await notificationService.ShowAsync(
                NotificationKey.Package_Install_Completed,
                new Notification
                {
                    Title = runner.ModificationCompleteTitle,
                    Body = runner.ModificationCompleteMessage
                }
            );
        }
    }
}
