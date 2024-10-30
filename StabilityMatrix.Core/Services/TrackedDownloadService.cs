using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Services;

public class TrackedDownloadService : ITrackedDownloadService, IDisposable
{
    private readonly ILogger<TrackedDownloadService> logger;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private readonly IModelIndexService modelIndexService;

    private readonly ConcurrentDictionary<Guid, (TrackedDownload Download, FileStream Stream)> downloads =
        new();
    private readonly ConcurrentQueue<TrackedDownload> pendingDownloads = new();
    private readonly SemaphoreSlim downloadSemaphore;

    public IEnumerable<TrackedDownload> Downloads => downloads.Values.Select(x => x.Download);
    public IEnumerable<TrackedDownload> PendingDownloads => pendingDownloads;

    /// <inheritdoc />
    public event EventHandler<TrackedDownload>? DownloadAdded;
    public event EventHandler<TrackedDownload>? DownloadStarted;

    private int MaxConcurrentDownloads { get; set; }

    private bool IsQueueEnabled => MaxConcurrentDownloads > 0;
    public int ActiveDownloads =>
        downloads.Count(kvp => kvp.Value.Download.ProgressState == ProgressState.Working);

    public TrackedDownloadService(
        ILogger<TrackedDownloadService> logger,
        IDownloadService downloadService,
        IModelIndexService modelIndexService,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
        this.modelIndexService = modelIndexService;

        // Index for in-progress downloads when library dir loaded
        settingsManager.RegisterOnLibraryDirSet(path =>
        {
            var downloadsDir = new DirectoryPath(settingsManager.DownloadsDirectory);
            // Ignore if not exist
            if (!downloadsDir.Exists)
                return;

            LoadInProgressDownloads(downloadsDir);
        });

        MaxConcurrentDownloads = settingsManager.Settings.MaxConcurrentDownloads;
        downloadSemaphore = new SemaphoreSlim(MaxConcurrentDownloads);
    }

    private void OnDownloadAdded(TrackedDownload download)
    {
        logger.LogInformation("Download added: ({Download}, {State})", download.Id, download.ProgressState);
        DownloadAdded?.Invoke(this, download);
    }

    private void OnDownloadStarted(TrackedDownload download)
    {
        logger.LogInformation("Download started: ({Download}, {State})", download.Id, download.ProgressState);
        DownloadStarted?.Invoke(this, download);
    }

    /// <summary>
    /// Creates a new tracked download with backed json file and adds it to the dictionary.
    /// </summary>
    /// <param name="download"></param>
    private void AddDownload(TrackedDownload download)
    {
        // Set download service
        download.SetDownloadService(downloadService);

        // Create json file
        var downloadsDir = new DirectoryPath(settingsManager.DownloadsDirectory);
        downloadsDir.Create();
        var jsonFile = downloadsDir.JoinFile($"{download.Id}.json");
        var jsonFileStream = jsonFile.Info.Open(FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);

        // Serialize to json
        var json = JsonSerializer.Serialize(download);
        jsonFileStream.Write(Encoding.UTF8.GetBytes(json));
        jsonFileStream.Flush();

        // Add to dictionary
        downloads.TryAdd(download.Id, (download, jsonFileStream));

        // Connect to state changed event to update json file
        AttachHandlers(download);

        OnDownloadAdded(download);
    }

    public async Task TryStartDownload(TrackedDownload download)
    {
        if (IsQueueEnabled && ActiveDownloads >= MaxConcurrentDownloads)
        {
            logger.LogDebug("Download {Download} is pending", download.FileName);
            pendingDownloads.Enqueue(download);
            download.SetPending();
            UpdateJsonForDownload(download);
            return;
        }

        if (!IsQueueEnabled || await downloadSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            logger.LogDebug("Starting download {Download}", download.FileName);
            download.Start();
            OnDownloadStarted(download);
        }
        else
        {
            logger.LogDebug("Download {Download} is pending", download.FileName);
            pendingDownloads.Enqueue(download);
            download.SetPending();
            UpdateJsonForDownload(download);
        }
    }

    public async Task TryResumeDownload(TrackedDownload download)
    {
        if (IsQueueEnabled && ActiveDownloads >= MaxConcurrentDownloads)
        {
            logger.LogDebug("Download {Download} is pending", download.FileName);
            pendingDownloads.Enqueue(download);
            download.SetPending();
            UpdateJsonForDownload(download);
            return;
        }

        if (!IsQueueEnabled || await downloadSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            logger.LogDebug("Resuming download {Download}", download.FileName);
            download.Resume();
            OnDownloadStarted(download);
        }
        else
        {
            logger.LogDebug("Download {Download} is pending", download.FileName);
            pendingDownloads.Enqueue(download);
            download.SetPending();
            UpdateJsonForDownload(download);
        }
    }

    public void UpdateMaxConcurrentDownloads(int newMax)
    {
        if (newMax <= 0)
        {
            MaxConcurrentDownloads = 0;
            return;
        }

        var oldMax = MaxConcurrentDownloads;
        MaxConcurrentDownloads = newMax;

        if (oldMax == newMax)
            return;

        logger.LogInformation("Updating max concurrent downloads from {OldMax} to {NewMax}", oldMax, newMax);

        if (newMax > oldMax)
        {
            downloadSemaphore.Release(newMax - oldMax);
            ProcessPendingDownloads().SafeFireAndForget();
        }
        // When reducing, we don't need to do anything immediately.
        // The system will naturally adjust as downloads complete or are paused/resumed.

        AdjustSemaphoreCount();
    }

    private void AdjustSemaphoreCount()
    {
        var currentCount = downloadSemaphore.CurrentCount;
        var targetCount = MaxConcurrentDownloads - ActiveDownloads;

        if (currentCount < targetCount)
        {
            downloadSemaphore.Release(targetCount - currentCount);
        }
        else if (currentCount > targetCount)
        {
            for (var i = 0; i < currentCount - targetCount; i++)
            {
                downloadSemaphore.Wait(0);
            }
        }
    }

    /// <summary>
    /// Update the json file for the download.
    /// </summary>
    private void UpdateJsonForDownload(TrackedDownload download)
    {
        // Serialize to json
        var json = JsonSerializer.Serialize(download);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        // Write to file
        var (_, fs) = downloads[download.Id];
        fs.Seek(0, SeekOrigin.Begin);
        fs.Write(jsonBytes);
        fs.Flush();
    }

    private void AttachHandlers(TrackedDownload download)
    {
        download.ProgressStateChanged += TrackedDownload_OnProgressStateChanged;
    }

    private async Task ProcessPendingDownloads()
    {
        while (pendingDownloads.TryPeek(out var nextDownload))
        {
            if (ActiveDownloads >= MaxConcurrentDownloads)
            {
                break;
            }

            if (pendingDownloads.TryDequeue(out nextDownload))
            {
                if (nextDownload.DownloadedBytes > 0)
                {
                    await TryResumeDownload(nextDownload).ConfigureAwait(false);
                }
                else
                {
                    await TryStartDownload(nextDownload).ConfigureAwait(false);
                }
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Handler when the download's state changes
    /// </summary>
    private void TrackedDownload_OnProgressStateChanged(object? sender, ProgressState e)
    {
        if (sender is not TrackedDownload download)
        {
            return;
        }

        // Update json file
        UpdateJsonForDownload(download);

        // If the download is completed, remove it from the dictionary and delete the json file
        if (e is ProgressState.Success or ProgressState.Failed or ProgressState.Cancelled)
        {
            if (downloads.TryRemove(download.Id, out var downloadInfo))
            {
                downloadInfo.Item2.Dispose();
                // Delete json file
                new DirectoryPath(settingsManager.DownloadsDirectory)
                    .JoinFile($"{download.Id}.json")
                    .Delete();
                logger.LogDebug("Removed download {Download}", download.FileName);

                if (IsQueueEnabled)
                {
                    try
                    {
                        downloadSemaphore.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        // Ignore
                    }

                    ProcessPendingDownloads().SafeFireAndForget();
                }
            }
        }
        else if (e is ProgressState.Paused && IsQueueEnabled)
        {
            downloadSemaphore.Release();
            ProcessPendingDownloads().SafeFireAndForget();
        }

        // On successes, run the continuation action
        if (e == ProgressState.Success)
        {
            if (download.ContextAction is not null)
            {
                logger.LogDebug("Running context action for {Download}", download.FileName);
            }

            switch (download.ContextAction)
            {
                case CivitPostDownloadContextAction action:
                    action.Invoke(settingsManager, modelIndexService);
                    break;
                case ModelPostDownloadContextAction action:
                    action.Invoke(modelIndexService);
                    break;
            }
        }
    }

    private void LoadInProgressDownloads(DirectoryPath downloadsDir)
    {
        logger.LogDebug("Indexing in-progress downloads at {DownloadsDir}...", downloadsDir);

        var jsonFiles = downloadsDir.Info.EnumerateFiles("*.json", EnumerationOptionConstants.TopLevelOnly);

        // Add to dictionary, the file name is the guid
        foreach (var file in jsonFiles)
        {
            // Try to get a shared write handle
            try
            {
                var fileStream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

                // Deserialize json and add to dictionary
                var download = JsonSerializer.Deserialize<TrackedDownload>(fileStream)!;

                // If the download is marked as working, pause it
                if (download.ProgressState is ProgressState.Working or ProgressState.Pending)
                {
                    download.ProgressState = ProgressState.Paused;
                }
                else if (
                    download.ProgressState != ProgressState.Inactive
                    && download.ProgressState != ProgressState.Paused
                    && download.ProgressState != ProgressState.Pending
                )
                {
                    // If the download is not inactive, skip it
                    logger.LogWarning(
                        "Skipping download {Download} with state {State}",
                        download.FileName,
                        download.ProgressState
                    );
                    fileStream.Dispose();

                    // Delete json file
                    logger.LogDebug(
                        "Deleting json file for {Download} with unsupported state",
                        download.FileName
                    );
                    file.Delete();
                    continue;
                }

                download.SetDownloadService(downloadService);

                downloads.TryAdd(download.Id, (download, fileStream));

                if (download.ProgressState == ProgressState.Pending)
                {
                    pendingDownloads.Enqueue(download);
                }

                AttachHandlers(download);

                OnDownloadAdded(download);

                logger.LogDebug("Loaded in-progress download {Download}", download.FileName);
            }
            catch (Exception e)
            {
                logger.LogInformation(e, "Could not open file {File} for reading", file.Name);
            }
        }
    }

    public TrackedDownload NewDownload(Uri downloadUrl, FilePath downloadPath)
    {
        var download = new TrackedDownload
        {
            Id = Guid.NewGuid(),
            SourceUrl = downloadUrl,
            DownloadDirectory = downloadPath.Directory!,
            FileName = downloadPath.Name,
            TempFileName = NewTempFileName(downloadPath.Directory!),
        };

        AddDownload(download);

        return download;
    }

    /// <summary>
    /// Generate a new temp file name that is unique in the given directory.
    /// In format of "Unconfirmed {id}.smdownload"
    /// </summary>
    /// <param name="parentDir"></param>
    /// <returns></returns>
    private static string NewTempFileName(DirectoryPath parentDir)
    {
        FilePath? tempFile = null;

        for (var i = 0; i < 10; i++)
        {
            if (tempFile is { Exists: false })
            {
                return tempFile.Name;
            }
            var id = Random.Shared.Next(1000000, 9999999);
            tempFile = parentDir.JoinFile($"Unconfirmed {id}.smdownload");
        }

        throw new Exception("Failed to generate a unique temp file name.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (download, fs) in downloads.Values)
        {
            if (download.ProgressState == ProgressState.Working)
            {
                try
                {
                    download.Pause();
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Failed to pause download {Download}", download.FileName);
                }
            }
        }

        GC.SuppressFinalize(this);
    }
}
