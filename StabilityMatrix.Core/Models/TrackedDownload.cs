using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using AsyncAwaitBestPractices;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models;

public class TrackedDownloadProgressEventArgs : EventArgs
{
    public ProgressReport Progress { get; init; }
    public ProgressState State { get; init; }
}

public class TrackedDownload
{
    [JsonIgnore]
    private IDownloadService? downloadService;
    
    [JsonIgnore]
    private Task? downloadTask;
    
    [JsonIgnore]
    private CancellationTokenSource? downloadCancellationTokenSource;
    
    [JsonIgnore]
    private CancellationTokenSource? downloadPauseTokenSource;
    
    private CancellationTokenSource AggregateCancellationTokenSource =>
        CancellationTokenSource.CreateLinkedTokenSource(
            downloadCancellationTokenSource?.Token ?? CancellationToken.None,
            downloadPauseTokenSource?.Token ?? CancellationToken.None);
    
    public required Guid Id { get; init; }
    
    public required Uri SourceUrl { get; init; }
    
    public Uri? RedirectedUrl { get; init; }
    
    public required DirectoryPath DownloadDirectory { get; init; }
    
    public required string FileName { get; init; }
    
    public required string TempFileName { get; init; }
    
    public string? ExpectedHashSha256 { get; init; }
    
    public bool ValidateHash { get; init; }

    public ProgressState ProgressState { get; private set; } = ProgressState.Inactive;

    public Exception? Exception { get; private set; }
    
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
            cancellationToken: cancellationToken).ConfigureAwait(false);
        
        // If hash validation is enabled, validate the hash
        if (ValidateHash)
        {
            var hash = await FileHash.GetSha256Async(DownloadDirectory.JoinFile(TempFileName), progress).ConfigureAwait(false);
            if (hash != ExpectedHashSha256)
            {
                throw new Exception($"Hash validation for {FileName} failed, expected {ExpectedHashSha256} but got {hash}");
            }
        }
    }
    
    public void Start()
    {
        if (ProgressState != ProgressState.Inactive)
        {
            throw new InvalidOperationException($"Download state must be inactive to start, not {ProgressState}");
        }
        
        EnsureDownloadService();
        
        downloadCancellationTokenSource = new CancellationTokenSource();
        downloadPauseTokenSource = new CancellationTokenSource();
        
        downloadTask = StartDownloadTask(0, AggregateCancellationTokenSource.Token)
            .ContinueWith(OnDownloadTaskCompleted);
    }

    public void Resume()
    {
        if (ProgressState != ProgressState.Inactive) return;

        EnsureDownloadService();
        
        downloadCancellationTokenSource = new CancellationTokenSource();
        downloadPauseTokenSource = new CancellationTokenSource();
        
        downloadTask = StartDownloadTask(0, AggregateCancellationTokenSource.Token)
            .ContinueWith(OnDownloadTaskCompleted);
    }

    public void Pause()
    {
        if (ProgressState != ProgressState.Working) return;
        
        downloadPauseTokenSource?.Cancel();
    }
    
    public void Cancel()
    {
        if (ProgressState is not (ProgressState.Working or ProgressState.Inactive)) return;
        
        downloadCancellationTokenSource?.Cancel();
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
                throw new InvalidOperationException("Download task was cancelled but neither cancellation token was cancelled.");
            }
        }
        // For faulted
        else if (task.IsFaulted)
        {
            // Set the exception
            Exception = task.Exception;
            
            // Delete the temp file
            try
            {
                DownloadDirectory.JoinFile(TempFileName).Delete();
            }
            catch (IOException)
            {
            }

            ProgressState = ProgressState.Failed;
        }
        // Otherwise success
        else
        {
            ProgressState = ProgressState.Success;
        }

        // For failed or cancelled, delete the temp file
        if (ProgressState is ProgressState.Failed or ProgressState.Cancelled)
        {
            // Delete the temp file
            try
            {
                DownloadDirectory.JoinFile(TempFileName).Delete();
            }
            catch (IOException)
            {
            }
        } 
        else if (ProgressState == ProgressState.Success)
        {
            // Move the temp file to the final file
            DownloadDirectory.JoinFile(TempFileName).MoveTo(DownloadDirectory.JoinFile(FileName));
        }
        
        // For pause, just do nothing
        
        OnProgressStateChanged(ProgressState);
        
        // Dispose of the task and cancellation token
        downloadTask?.Dispose();
        downloadTask = null;
        downloadCancellationTokenSource?.Dispose();
        downloadCancellationTokenSource = null;
    }
    
    public void SetDownloadService(IDownloadService service)
    {
        downloadService = service;
    }
}
