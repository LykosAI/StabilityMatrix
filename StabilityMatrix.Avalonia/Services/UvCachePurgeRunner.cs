using Avalonia.Controls.Notifications;
using Avalonia.Threading;
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

    private static void PostProgress(Guid progressId, ProgressReport report, bool failed = false)
    {
        // Progress may arrive from a background thread; the Activity panel's collection
        // must only be touched from the UI thread
        Dispatcher.UIThread.Post(() =>
            EventManager.Instance.OnProgressChanged(new ProgressItem(progressId, "uv Cache", report, failed))
        );
    }
}
