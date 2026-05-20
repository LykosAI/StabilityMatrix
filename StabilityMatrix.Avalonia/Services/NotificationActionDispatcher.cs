using Avalonia.Threading;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Notifications;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<INotificationActionDispatcher, NotificationActionDispatcher>]
public class NotificationActionDispatcher(
    ILogger<NotificationActionDispatcher> logger,
    INavigationService<MainWindowViewModel> navigationService
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
