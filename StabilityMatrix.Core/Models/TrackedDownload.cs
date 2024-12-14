using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using AsyncAwaitBestPractices;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models;

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

    /// <summary>
    /// Whether to auto-extract the archive after download
    /// </summary>
    public bool AutoExtractArchive { get; set; }

    /// <summary>
    /// Optional relative path to extract the archive to, if AutoExtractArchive is true
    /// </summary>
    public string? ExtractRelativePath { get; set; }

    [JsonIgnore]
    [MemberNotNullWhen(true, nameof(ExpectedHashSha256))]
    public bool ValidateHash => ExpectedHashSha256 is not null;

    [JsonConverter(typeof(JsonStringEnumConverter<ProgressState>))]
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
    public event EventHandler<ProgressReport>? ProgressUpdate;

    private void OnProgressUpdate(ProgressReport e)
    {
        // Update downloaded and total bytes
        DownloadedBytes = Convert.ToInt64(e.Current);
        TotalBytes = Convert.ToInt64(e.Total);

        ProgressUpdate?.Invoke(this, e);
    }

    public event EventHandler<ProgressState>? ProgressStateChanging;

    private void OnProgressStateChanging(ProgressState e)
    {
        Logger.Debug("Download {Download}: State changing to {State}", FileName, e);

        ProgressStateChanging?.Invoke(this, e);
    }

    public event EventHandler<ProgressState>? ProgressStateChanged;

    private void OnProgressStateChanged(ProgressState e)
    {
        Logger.Debug("Download {Download}: State changed to {State}", FileName, e);

        ProgressStateChanged?.Invoke(this, e);
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

        DownloadDirectory.Create();

        await downloadService!
            .ResumeDownloadToFileAsync(
                SourceUrl.ToString(),
                DownloadDirectory.JoinFile(TempFileName),
                resumeFromByte,
                progress,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        // If hash validation is enabled, validate the hash
        if (ValidateHash)
        {
            OnProgressUpdate(new ProgressReport(0, isIndeterminate: true, type: ProgressType.Hashing));
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

        // Rename the temp file to the final file
        var tempFile = DownloadDirectory.JoinFile(TempFileName);
        var finalFile = tempFile.Rename(FileName);

        // If auto-extract is enabled, extract the archive
        if (AutoExtractArchive)
        {
            OnProgressUpdate(new ProgressReport(0, isIndeterminate: true, type: ProgressType.Extract));

            var extractDirectory = string.IsNullOrWhiteSpace(ExtractRelativePath)
                ? DownloadDirectory
                : DownloadDirectory.JoinDir(ExtractRelativePath);

            extractDirectory.Create();

            await ArchiveHelper
                .Extract7Z(finalFile, extractDirectory, new Progress<ProgressReport>(OnProgressUpdate))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// This is only intended for use by the download service.
    /// Please use <see cref="TrackedDownloadService"/>.TryStartDownload instead.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    internal void Start()
    {
        if (ProgressState != ProgressState.Inactive && ProgressState != ProgressState.Pending)
        {
            throw new InvalidOperationException(
                $"Download state must be inactive or pending to start, not {ProgressState}"
            );
        }
        Logger.Debug("Starting download {Download}", FileName);

        EnsureDownloadService();

        downloadCancellationTokenSource = new CancellationTokenSource();
        downloadPauseTokenSource = new CancellationTokenSource();

        downloadTask = StartDownloadTask(0, AggregateCancellationTokenSource.Token)
            .ContinueWith(OnDownloadTaskCompleted);

        OnProgressStateChanging(ProgressState.Working);
        ProgressState = ProgressState.Working;
        OnProgressStateChanged(ProgressState);
    }

    internal void Resume()
    {
        if (ProgressState != ProgressState.Inactive && ProgressState != ProgressState.Paused)
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

        OnProgressStateChanging(ProgressState.Working);
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
        OnProgressStateChanging(ProgressState.Paused);
        ProgressState = ProgressState.Paused;
        OnProgressStateChanged(ProgressState);
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

            OnProgressStateChanging(ProgressState.Cancelled);
            ProgressState = ProgressState.Cancelled;
            OnProgressStateChanged(ProgressState);
        }
    }

    public void SetPending()
    {
        OnProgressStateChanging(ProgressState.Pending);
        ProgressState = ProgressState.Pending;
        OnProgressStateChanged(ProgressState);
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
                OnProgressStateChanging(ProgressState.Cancelled);
                ProgressState = ProgressState.Cancelled;
            }
            // If the task was not cancelled, set the state to paused
            else if (downloadPauseTokenSource?.IsCancellationRequested == true)
            {
                OnProgressStateChanging(ProgressState.Inactive);
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

            if ((Exception is IOException || Exception?.InnerException is IOException) && attempts < 3)
            {
                attempts++;
                Logger.Warn(
                    "Download {Download} failed with {Exception}, retrying ({Attempt})",
                    FileName,
                    Exception,
                    attempts
                );

                OnProgressStateChanging(ProgressState.Inactive);
                ProgressState = ProgressState.Inactive;
                Resume();
                return;
            }

            Logger.Warn(Exception, "Download {Download} failed", FileName);

            OnProgressStateChanging(ProgressState.Failed);
            ProgressState = ProgressState.Failed;
        }
        // Otherwise success
        else
        {
            OnProgressStateChanging(ProgressState.Success);
            ProgressState = ProgressState.Success;
        }

        // For failed or cancelled, delete the temp files
        if (ProgressState is ProgressState.Failed or ProgressState.Cancelled)
        {
            DoCleanup();
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
