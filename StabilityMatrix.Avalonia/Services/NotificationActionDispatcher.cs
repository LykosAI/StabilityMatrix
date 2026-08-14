using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Notifications;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<INotificationActionDispatcher, NotificationActionDispatcher>]
public class NotificationActionDispatcher(
    ILogger<NotificationActionDispatcher> logger,
    INavigationService<MainWindowViewModel> navigationService,
    IUvCacheService uvCacheService,
    Lazy<INotificationService> notificationService
) : INotificationActionDispatcher
{
    public async Task DispatchAsync(NotificationAction action)
    {
        try
        {
            switch (action)
            {
                case OpenFolderAction openFolder:
                    if (!string.IsNullOrWhiteSpace(openFolder.Path))
                    {
                        await ProcessRunner.OpenFolderBrowser(openFolder.Path);
                    }
                    break;

                case NavigateToPageAction nav:
                    var type = Type.GetType(nav.PageTypeName);
                    if (type == null)
                    {
                        logger.LogWarning(
                            "NavigateToPageAction could not resolve type {Name}",
                            nav.PageTypeName
                        );
                        break;
                    }
                    await Dispatcher.UIThread.InvokeAsync(() => navigationService.NavigateTo(type));
                    break;

                case ToggleProgressFlyoutAction:
                    await Dispatcher.UIThread.InvokeAsync(() => EventManager.Instance.OnShowProgressFlyout());
                    break;

                case ShowChangelogAction:
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        EventManager.Instance.OnShowChangelogRequested()
                    );
                    break;

                case PurgeUvCacheAction:
                    var freedBytes = await uvCacheService.PurgeAsync();
                    notificationService.Value.Show(
                        "uv cache purged",
                        $"Freed {Size.FormatBytes((ulong)freedBytes)}",
                        NotificationType.Success
                    );
                    break;

                default:
                    logger.LogWarning("Unknown notification action {Action}", action);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to dispatch notification action {Action}", action);
        }
    }
}
