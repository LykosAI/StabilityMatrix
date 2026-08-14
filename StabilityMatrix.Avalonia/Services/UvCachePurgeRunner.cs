using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

public interface IUvCachePurgeRunner
{
    /// <summary>
    /// Purges the uv cache with a progress item in the Activity panel and a result toast.
    /// Only one purge runs at a time; concurrent calls return 0 without doing anything.
    /// Returns the number of bytes freed.
    /// </summary>
    Task<long> RunPurgeAsync();

    /// <summary>
    /// Shows a plain-language confirmation dialog (pass the last known cache size for the
    /// dialog text, or 0 when unknown), then purges as <see cref="RunPurgeAsync"/>.
    /// Returns the number of bytes freed, or 0 when the user cancels.
    /// </summary>
    Task<long> RunPurgeWithConfirmAsync(long sizeBytes);
}

[RegisterSingleton<IUvCachePurgeRunner, UvCachePurgeRunner>]
public class UvCachePurgeRunner(
    IUvCacheService uvCacheService,
    Lazy<INotificationService> notificationService
) : IUvCachePurgeRunner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private int isRunning;

    /// <inheritdoc />
    public async Task<long> RunPurgeAsync()
    {
        if (Interlocked.Exchange(ref isRunning, 1) == 1)
        {
            Logger.Info("uv cache purge already running, ignoring request");
            return 0;
        }

        var progressId = Guid.NewGuid();

        try
        {
            var freed = await uvCacheService.PurgeAsync(
                new Progress<ProgressReport>(report => PostProgress(progressId, report))
            );

            Dispatcher.UIThread.Post(() =>
                notificationService.Value.Show(
                    "uv cache cleared",
                    $"Freed {Size.FormatBase10Bytes(freed)}",
                    NotificationType.Success
                )
            );

            return freed;
        }
        catch (Exception e)
        {
            Logger.Error(e, "uv cache purge failed");

            PostProgress(progressId, new ProgressReport(1d, message: "Clear failed"), failed: true);

            Dispatcher.UIThread.Post(() =>
                notificationService.Value.Show(
                    "Couldn't clear the uv cache",
                    "See the logs for details, or try again from Settings.",
                    NotificationType.Error
                )
            );

            return 0;
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }

    /// <inheritdoc />
    public async Task<long> RunPurgeWithConfirmAsync(long sizeBytes)
    {
        var confirmed = await Dispatcher.UIThread.InvokeAsync(() => ShowConfirmDialogAsync(sizeBytes));

        return confirmed ? await RunPurgeAsync() : 0;
    }

    /// <summary>
    /// Explains what clearing the uv cache means before doing it.
    /// Returns true when the user confirms.
    /// </summary>
    private static async Task<bool> ShowConfirmDialogAsync(long sizeBytes)
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

    private static void PostProgress(Guid progressId, ProgressReport report, bool failed = false)
    {
        // Progress may arrive from a background thread; the Activity panel's collection
        // must only be touched from the UI thread
        Dispatcher.UIThread.Post(() =>
            EventManager.Instance.OnProgressChanged(new ProgressItem(progressId, "uv Cache", report, failed))
        );
    }
}
