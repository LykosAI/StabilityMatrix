using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using AsyncAwaitBestPractices;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models;

[JsonSerializable(typeof(TrackedDownload))]
public class TrackedDownload
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [JsonIgnore]
    private IDownloadService? downloadService;

    [JsonIgnore]
    private Task? downloadTask;

    [JsonIgnore]
    private CancellationTokenSource? downloadCancellationTokenSource;

    [JsonIgnore]
    private CancellationTokenSource? downloadPauseTokenSource;

    [JsonIgnore]
    private CancellationTokenSource AggregateCancellationTokenSource =>
        CancellationTokenSource.CreateLinkedTokenSource(
            downloadCancellationTokenSource?.Token ?? CancellationToken.None,
            downloadPauseTokenSource?.Token ?? CancellationToken.None
        );

    public required Guid Id { get; init; }

    public required Uri SourceUrl { get; init; }

    public Uri? RedirectedUrl { get; init; }

    public required DirectoryPath DownloadDirectory { get; init; }

    public required string FileName { get; init; }

    public required string TempFileName { get; init; }

    public string? ExpectedHashSha256 { get; set; }

    [JsonIgnore]
    [MemberNotNullWhen(true, nameof(ExpectedHashSha256))]
    public bool ValidateHash => ExpectedHashSha256 is not null;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProgressState ProgressState { get; set; } = ProgressState.Inactive;

    public List<string> ExtraCleanupFileNames { get; init; } = new();

    // Used for restoring progress on load
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }

    /// <summary>
    /// Optional context action to be invoked on completion
    /// </summary>
    public IContextAction? ContextAction { get; set; }

    [JsonIgnore]
    public Exception? Exception { get; private set; }

    private int attempts;

    #region Events
    private WeakEventManager<ProgressReport>? progressUpdateEventManager;

    public event EventHandler<ProgressReport> ProgressUpdate
    {
        add
        {
            progressUpdateEventManager ??= new WeakEventManager<ProgressReport>();
            progressUpdateEventManager.AddEventHandler(value);
        }
        remove => progressUpdateEventManager?.RemoveEventHandler(value);
    }

    protected void OnProgressUpdate(ProgressReport e)
    {
        // Update downloaded and total bytes
        DownloadedBytes = Convert.ToInt64(e.Current);
        TotalBytes = Convert.ToInt64(e.Total);

        progressUpdateEventManager?.RaiseEvent(this, e, nameof(ProgressUpdate));
    }

    private WeakEventManager<ProgressState>? progressStateChangedEventManager;

    public event EventHandler<ProgressState> ProgressStateChanged
    {
        add
        {
            progressStateChangedEventManager ??= new WeakEventManager<ProgressState>();
            progressStateChangedEventManager.AddEventHandler(value);
        }
        remove => progressStateChangedEventManager?.RemoveEventHandler(value);
    }

    protected void OnProgressStateChanged(ProgressState e)
    {
        progressStateChangedEventManager?.RaiseEvent(this, e, nameof(ProgressStateChanged));
    }
    #endregion

    [MemberNotNull(nameof(downloadService))]
    private void EnsureDownloadService()
    {
        if (downloadService == null)
        {
            throw new InvalidOperationException("Download service is not set.");
        }
    }

    private async Task StartDownloadTask(long resumeFromByte, CancellationToken cancellationToken)
    {
        var progress = new Progress<ProgressReport>(OnProgressUpdate);

        await downloadService!.ResumeDownloadToFileAsync(
            SourceUrl.ToString(),
            DownloadDirectory.JoinFile(TempFileName),
            resumeFromByte,
            progress,
            cancellationToken: cancellationToken
        );

        // If hash validation is enabled, validate the hash
        if (ValidateHash)
        {
            OnProgressUpdate(
                new ProgressReport(0, isIndeterminate: true, type: ProgressType.Hashing)
            );
            var hash = await FileHash
                .GetSha256Async(DownloadDirectory.JoinFile(TempFileName), progress)
                .ConfigureAwait(false);
            if (hash != ExpectedHashSha256?.ToLowerInvariant())
            {
                throw new Exception(
                    $"Hash validation for {FileName} failed, expected {ExpectedHashSha256} but got {hash}"
                );
            }
        }
    }

    public void Start()
    {
        if (ProgressState != ProgressState.Inactive)
        {
            throw new InvalidOperationException(
                $"Download state must be inactive to start, not {ProgressState}"
            );
        }
        Logger.Debug("Starting download {Download}", FileName);

        EnsureDownloadService();

        downloadCancellationTokenSource = new CancellationTokenSource();
        downloadPauseTokenSource = new CancellationTokenSource();

        downloadTask = StartDownloadTask(0, AggregateCancellationTokenSource.Token)
            .ContinueWith(OnDownloadTaskCompleted);

        ProgressState = ProgressState.Working;
        OnProgressStateChanged(ProgressState);
    }

    public void Resume()
    {
        if (ProgressState != ProgressState.Inactive)
        {
            Logger.Warn(
                "Attempted to resume download {Download} but it is not paused ({State})",
                FileName,
                ProgressState
            );
        }
        Logger.Debug("Resuming download {Download}", FileName);

        // Read the temp file to get the current size
        var tempSize = 0L;

        var tempFile = DownloadDirectory.JoinFile(TempFileName);
        if (tempFile.Exists)
        {
            tempSize = tempFile.Info.Length;
        }

        EnsureDownloadService();

        downloadCancellationTokenSource = new CancellationTokenSource();
        downloadPauseTokenSource = new CancellationTokenSource();

        downloadTask = StartDownloadTask(tempSize, AggregateCancellationTokenSource.Token)
            .ContinueWith(OnDownloadTaskCompleted);

        ProgressState = ProgressState.Working;
        OnProgressStateChanged(ProgressState);
    }

    public void Pause()
    {
        if (ProgressState != ProgressState.Working)
        {
            Logger.Warn(
                "Attempted to pause download {Download} but it is not in progress ({State})",
                FileName,
                ProgressState
            );
            return;
        }

        Logger.Debug("Pausing download {Download}", FileName);
        downloadPauseTokenSource?.Cancel();
    }

    public void Cancel()
    {
        if (ProgressState is not (ProgressState.Working or ProgressState.Inactive))
        {
            Logger.Warn(
                "Attempted to cancel download {Download} but it is not in progress ({State})",
                FileName,
                ProgressState
            );
            return;
        }

        Logger.Debug("Cancelling download {Download}", FileName);

        // Cancel token if it exists
        if (downloadCancellationTokenSource is { } token)
        {
            token.Cancel();
        }
        // Otherwise handle it manually
        else
        {
            DoCleanup();

            ProgressState = ProgressState.Cancelled;
            OnProgressStateChanged(ProgressState);
        }
    }

    /// <summary>
    /// Deletes the temp file and any extra cleanup files
    /// </summary>
    private void DoCleanup()
    {
        try
        {
            DownloadDirectory.JoinFile(TempFileName).Delete();
        }
        catch (IOException)
        {
            Logger.Warn("Failed to delete temp file {TempFile}", TempFileName);
        }

        foreach (var extraFile in ExtraCleanupFileNames)
        {
            try
            {
                DownloadDirectory.JoinFile(extraFile).Delete();
            }
            catch (IOException)
            {
                Logger.Warn("Failed to delete extra cleanup file {ExtraFile}", extraFile);
            }
        }
    }

    /// <summary>
    /// Invoked by the task's completion callback
    /// </summary>
    private void OnDownloadTaskCompleted(Task task)
    {
        // For cancelled, check if it was actually cancelled or paused
        if (task.IsCanceled)
        {
            // If the task was cancelled, set the state to cancelled
            if (downloadCancellationTokenSource?.IsCancellationRequested == true)
            {
                ProgressState = ProgressState.Cancelled;
            }
            // If the task was not cancelled, set the state to paused
            else if (downloadPauseTokenSource?.IsCancellationRequested == true)
            {
                ProgressState = ProgressState.Inactive;
            }
            else
            {
                throw new InvalidOperationException(
                    "Download task was cancelled but neither cancellation token was cancelled."
                );
            }
        }
        // For faulted
        else if (task.IsFaulted)
        {
            // Set the exception
            Exception = task.Exception;

            if (
                (Exception is IOException || Exception?.InnerException is IOException)
                && attempts < 3
            )
            {
                attempts++;
                Logger.Warn(
                    "Download {Download} failed with {Exception}, retrying ({Attempt})",
                    FileName,
                    Exception,
                    attempts
                );
                ProgressState = ProgressState.Inactive;
                Resume();
                return;
            }

            ProgressState = ProgressState.Failed;
        }
        // Otherwise success
        else
        {
            ProgressState = ProgressState.Success;
        }

        // For failed or cancelled, delete the temp files
        if (ProgressState is ProgressState.Failed or ProgressState.Cancelled)
        {
            DoCleanup();
        }
        else if (ProgressState == ProgressState.Success)
        {
            // Move the temp file to the final file
            DownloadDirectory.JoinFile(TempFileName).MoveTo(DownloadDirectory.JoinFile(FileName));
        }

        // For pause, just do nothing

        OnProgressStateChanged(ProgressState);

        // Dispose of the task and cancellation token
        downloadTask = null;
        downloadCancellationTokenSource = null;
        downloadPauseTokenSource = null;
    }

    public void SetDownloadService(IDownloadService service)
    {
        downloadService = service;
    }
}
