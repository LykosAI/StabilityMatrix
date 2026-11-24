using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Languages;
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
    /// Returns the number of bytes freed, or null when the user cancels.
    /// </summary>
    Task<long?> RunPurgeWithConfirmAsync(long sizeBytes);
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
                new Progress<ProgressReport>(report => PostProgress(progressId, WithText(report)))
            );

            var freedText = Size.FormatBase10Bytes(freed);

            PostProgress(
                progressId,
                new ProgressReport(current: 1, total: 1)
                {
                    Message = string.Format(Resources.TextTemplate_UvCacheFreed, freedText),
                }
            );

            Dispatcher.UIThread.Post(() =>
                notificationService.Value.Show(
                    Resources.Label_UvCacheCleared,
                    string.Format(Resources.TextTemplate_UvCacheFreed, freedText),
                    NotificationType.Success
                )
            );

            return freed;
        }
        catch (Exception e)
        {
            Logger.Error(e, "uv cache purge failed");

            PostProgress(
                progressId,
                new ProgressReport(1d) { Message = Resources.Label_UvCacheClearFailed },
                failed: true
            );

            Dispatcher.UIThread.Post(() =>
                notificationService.Value.Show(
                    Resources.Label_UvCacheClearFailed,
                    Resources.Label_UvCacheClearFailed_Details,
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
    public async Task<long?> RunPurgeWithConfirmAsync(long sizeBytes)
    {
        var confirmed = await Dispatcher.UIThread.InvokeAsync(() => ShowConfirmDialogAsync(sizeBytes));

        if (!confirmed)
        {
            return null;
        }

        return await RunPurgeAsync();
    }

    /// <summary>
    /// Explains what clearing the uv cache means before doing it.
    /// Returns true when the user confirms.
    /// </summary>
    private static async Task<bool> ShowConfirmDialogAsync(long sizeBytes)
    {
        var dialog = DialogHelper.CreateTaskDialog(
            Resources.Label_ClearUvCacheConfirmTitle,
            sizeBytes > 0
                ? string.Format(
                    Resources.TextTemplate_ClearUvCacheConfirmSized,
                    Size.FormatBase10Bytes(sizeBytes)
                )
                : Resources.Label_ClearUvCacheConfirm
        );

        dialog.ShowProgressBar = false;
        dialog.Buttons = new List<TaskDialogButton>
        {
            new(Resources.Action_ClearCache, TaskDialogStandardResult.Yes) { IsDefault = true },
            TaskDialogButton.CancelButton,
        };

        return (TaskDialogStandardResult)await dialog.ShowAsync(true) == TaskDialogStandardResult.Yes;
    }

    /// <summary>
    /// The service reports numeric progress only; attach the localized display text here.
    /// </summary>
    private static ProgressReport WithText(ProgressReport report) =>
        report.IsIndeterminate
            ? report with
            {
                Message = Resources.Label_ClearingUvCache,
            }
            : report with
            {
                Message = string.Format(
                    Resources.TextTemplate_UvCacheClearedOfTotal,
                    Size.FormatBase10Bytes((long)(report.Current ?? 0)),
                    Size.FormatBase10Bytes((long)(report.Total ?? 0))
                ),
            };

    private static void PostProgress(Guid progressId, ProgressReport report, bool failed = false)
    {
        // Progress may arrive from a background thread; the Activity panel's collection
        // must only be touched from the UI thread
        Dispatcher.UIThread.Post(() =>
            EventManager.Instance.OnProgressChanged(
                new ProgressItem(progressId, Resources.Label_UvCache, report, failed)
            )
        );
    }
}
