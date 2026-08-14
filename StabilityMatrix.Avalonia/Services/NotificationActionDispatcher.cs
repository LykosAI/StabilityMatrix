using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
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
    INavigationService<MainWindowViewModel> navigationService,
    IUvCachePurgeRunner uvCachePurgeRunner
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

                case PurgeUvCacheAction purgeAction:
                    var confirmed = await Dispatcher.UIThread.InvokeAsync(() =>
                        ShowPurgeConfirmDialogAsync(purgeAction.SizeBytes)
                    );

                    if (confirmed)
                    {
                        await uvCachePurgeRunner.RunPurgeAsync();
                    }
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

    /// <summary>
    /// Explains what clearing the uv cache means before doing it.
    /// Returns true when the user confirms.
    /// </summary>
    private static async Task<bool> ShowPurgeConfirmDialogAsync(long sizeBytes)
    {
        var sizeText = sizeBytes > 0 ? $" about {Size.FormatBase10Bytes(sizeBytes)} of" : "";

        var dialog = DialogHelper.CreateTaskDialog(
            "Clear the uv cache?",
            $"This frees{sizeText} disk space used by cached package downloads. "
                + "Nothing you have installed is removed, but the next package install or "
                + "update will re-download anything it needs, so it may take a bit longer.\n\n"
                + "Large caches can take a few minutes to clear. You can keep using "
                + "Stability Matrix while it runs — progress shows in the Activity panel."
        );

        dialog.ShowProgressBar = false;
        dialog.Buttons = new List<TaskDialogButton>
        {
            new("Clear Cache", TaskDialogStandardResult.Yes) { IsDefault = true },
            TaskDialogButton.CancelButton,
        };

        return (TaskDialogStandardResult)await dialog.ShowAsync(true) == TaskDialogStandardResult.Yes;
    }
}
