using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<IPromptQueueService, PromptQueueService>]
public partial class PromptQueueService : ObservableObject, IPromptQueueService, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string QueueFileName = "prompt-queue.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly ISettingsManager settingsManager;
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private readonly IInferenceClientManager clientManager;
    private readonly INotificationService notificationService;

    private readonly object saveLock = new();
    private volatile CancellationTokenSource? delayedSaveCts;

    private volatile bool isLoaded;

    /// <summary>True when a debounced save has been requested but not yet written.</summary>
    private volatile bool savePending;

    /// <summary>Cancellation for the currently running item's generation.</summary>
    private CancellationTokenSource? currentItemCts;

    /// <summary>Set when StopProcessing is requested; the current item finishes but no next starts.</summary>
    private volatile bool stopRequested;

    public ObservableCollection<PromptQueueItem> Items { get; } = [];

    [ObservableProperty]
    private bool isProcessing;

    [ObservableProperty]
    private PromptQueueItem? currentItem;

    public PromptQueueService(
        ISettingsManager settingsManager,
        IServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager clientManager,
        INotificationService notificationService
    )
    {
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;
        this.clientManager = clientManager;
        this.notificationService = notificationService;

        settingsManager.RegisterOnLibraryDirSet(_ =>
            LoadAsync().SafeFireAndForget(ex => Logger.Warn(ex, "Failed to load prompt queue"))
        );

        Items.CollectionChanged += (_, _) => RequestSave();
    }

    private FilePath QueueFile => settingsManager.LibraryDir.JoinFile(QueueFileName);

    public void Enqueue(PromptQueueItem item)
    {
        AttachItemHandlers(item);
        Items.Add(item);
    }

    public void Remove(PromptQueueItem item)
    {
        if (item == CurrentItem)
        {
            // End-of-processing cleanup disposes the preview once the generation stops.
            CancelCurrent();
        }
        else
        {
            DisposePreview(item);
        }

        DetachItemHandlers(item);
        Items.Remove(item);
    }

    public void Requeue(PromptQueueItem item)
    {
        // Only finished items can be re-run; Running/Pending are no-ops
        if (!IsFinished(item))
            return;

        DisposePreview(item);

        item.Status = PromptQueueItemStatus.Pending;
        item.Progress = 0;
        item.ProgressText = null;
        item.ErrorMessage = null;
        item.StartedAt = null;
        item.CompletedAt = null;
        item.OutputImagePaths = [];
    }

    public void MoveUp(PromptQueueItem item)
    {
        var index = Items.IndexOf(item);
        if (index > 0)
        {
            Items.Move(index, index - 1);
        }
    }

    public void MoveDown(PromptQueueItem item)
    {
        var index = Items.IndexOf(item);
        if (index >= 0 && index < Items.Count - 1)
        {
            Items.Move(index, index + 1);
        }
    }

    public void ClearFinished()
    {
        foreach (var item in Items.Where(IsFinished).ToList())
        {
            DisposePreview(item);
            DetachItemHandlers(item);
            Items.Remove(item);
        }
    }

    public void ClearPending()
    {
        foreach (var item in Items.Where(x => x.Status == PromptQueueItemStatus.Pending).ToList())
        {
            DisposePreview(item);
            DetachItemHandlers(item);
            Items.Remove(item);
        }
    }

    private static bool IsFinished(PromptQueueItem item) =>
        item.Status
            is PromptQueueItemStatus.Completed
                or PromptQueueItemStatus.Failed
                or PromptQueueItemStatus.Cancelled;

    private static void DisposePreview(PromptQueueItem item)
    {
        var preview = item.PreviewImage;
        item.PreviewImage = null;
        preview?.Dispose();
    }

    #region Processing

    public void StartProcessing()
    {
        if (IsProcessing)
            return;

        // Called on the UI thread from the Start command - check connectivity up front so the
        // user gets instant feedback instead of the loop exiting in the background.
        if (!clientManager.IsConnected)
        {
            if (Items.Any(x => x.Status == PromptQueueItemStatus.Pending))
            {
                notificationService.Show(
                    Resources.Label_PromptQueue,
                    Resources.Label_PromptQueueNotConnected,
                    NotificationType.Warning
                );
            }

            return;
        }

        stopRequested = false;
        IsProcessing = true;

        ProcessLoopAsync()
            .SafeFireAndForget(ex =>
            {
                Logger.Error(ex, "Prompt queue processing loop faulted");
                Dispatcher.UIThread.Post(() => IsProcessing = false);
            });
    }

    public void StopProcessing()
    {
        stopRequested = true;
    }

    public void CancelCurrent()
    {
        try
        {
            currentItemCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The item finished and its CTS was disposed between the read and the cancel
        }
    }

    private async Task ProcessLoopAsync()
    {
        try
        {
            while (!stopRequested)
            {
                // Items is UI-owned; read the next pending item on the UI thread.
                var item = await Dispatcher
                    .UIThread.InvokeAsync(() =>
                        Items.FirstOrDefault(x => x.Status == PromptQueueItemStatus.Pending)
                    )
                    .GetTask()
                    .ConfigureAwait(false);
                if (item is null)
                    break;

                // Client disconnected mid-queue: notify once (on the UI thread - this loop runs
                // in the background), leave the item pending, and stop processing.
                if (!clientManager.IsConnected)
                {
                    await Dispatcher
                        .UIThread.InvokeAsync(() =>
                            notificationService.Show(
                                Resources.Label_PromptQueue,
                                Resources.Label_PromptQueueNotConnected,
                                NotificationType.Warning
                            )
                        )
                        .GetTask()
                        .ConfigureAwait(false);
                    break;
                }

                try
                {
                    await ProcessItemAsync(item).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // A per-item infrastructure failure must not kill the whole queue
                    Logger.Error(e, "Unexpected error processing prompt queue item {Id}", item.Id);
                    SetFailed(item, e.Message);
                }
            }
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentItem = null;
                IsProcessing = false;
            });
        }
    }

    private async Task ProcessItemAsync(PromptQueueItem item)
    {
        var scope = vmFactory.CreateScope();
        try
        {
            await ProcessItemCoreAsync(scope, item).ConfigureAwait(false);
        }
        finally
        {
            // Dispose the scope on the UI thread: view model Dispose implementations may be
            // UI-affine, and real tab scopes are also disposed on the UI thread.
            await Dispatcher.UIThread.InvokeAsync(scope.Dispose).GetTask().ConfigureAwait(false);
        }
    }

    private async Task ProcessItemCoreAsync(IServiceManagerScope<ViewModelBase> scope, PromptQueueItem item)
    {
        if (item.ProjectType.ToViewModelType() is not { } vmType)
        {
            SetFailed(item, $"Unsupported project type: {item.ProjectType}");
            return;
        }

        // Resolve and initialize the view model on the UI thread: tab view model constructors
        // capture SynchronizationContext.Current (real tabs are also constructed on the UI thread).
        InferenceGenerationViewModelBase? generationViewModel = null;

        await Dispatcher
            .UIThread.InvokeAsync(() =>
            {
                if (scope.ServiceManager.Get(vmType) is not InferenceGenerationViewModelBase vm)
                    return;

                if (item.Project.State is { } state)
                {
                    vm.LoadStateFromJsonObject(state);
                }

                item.Status = PromptQueueItemStatus.Running;
                item.StartedAt = DateTimeOffset.Now;
                item.Progress = 0;
                item.ProgressText = null;
                item.ErrorMessage = null;
                CurrentItem = item;

                generationViewModel = vm;
            })
            .GetTask()
            .ConfigureAwait(false);

        if (generationViewModel is null)
        {
            SetFailed(item, $"Could not create view model for {item.ProjectType}");
            return;
        }

        using var cts = new CancellationTokenSource();
        currentItemCts = cts;

        var progressMirror = new ProgressMirror(generationViewModel, item);
        var succeeded = false;

        try
        {
            // Start the generation on the UI thread so it runs with interactive semantics:
            // RunGeneration touches gallery/progress state after awaits and is only safe when
            // its continuations resume on the dispatcher context. The loop thread just awaits
            // the returned task.
            var generationTask = await Dispatcher
                .UIThread.InvokeAsync<Task>(() => generationViewModel.RunGenerationForQueueAsync(cts.Token))
                .GetTask()
                .ConfigureAwait(false);

            await generationTask.ConfigureAwait(false);

            await Dispatcher
                .UIThread.InvokeAsync(() =>
                {
                    // ImageSources is UI-owned; capture on the UI thread.
                    CaptureOutputs(generationViewModel, item);

                    item.Status = PromptQueueItemStatus.Completed;
                    item.Progress = 100;
                    item.ProgressText = null;
                    item.CompletedAt = DateTimeOffset.Now;
                })
                .GetTask()
                .ConfigureAwait(false);

            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            await Dispatcher
                .UIThread.InvokeAsync(() =>
                {
                    item.Status = PromptQueueItemStatus.Cancelled;
                    item.CompletedAt = DateTimeOffset.Now;
                    item.ProgressText = null;
                })
                .GetTask()
                .ConfigureAwait(false);
        }
        catch (ComfyNodeException e)
        {
            Logger.Warn(e, "Prompt queue item {Id} failed on node execution", item.Id);
            SetFailed(item, FormatNodeError(e));
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Prompt queue item {Id} failed", item.Id);
            SetFailed(item, e.Message);
        }
        finally
        {
            progressMirror.Dispose();
            currentItemCts = null;

            await Dispatcher
                .UIThread.InvokeAsync(() =>
                {
                    // Safe to dispose: RunGeneration's cleanup has already cleared the gallery's
                    // shared reference to this bitmap.
                    DisposePreview(item);
                    if (CurrentItem == item)
                        CurrentItem = null;
                })
                .GetTask()
                .ConfigureAwait(false);
        }

        // After the live preview is cleaned up, show the finished image on the card
        if (succeeded)
        {
            LoadOutputThumbnailAsync(item)
                .SafeFireAndForget(ex =>
                    Logger.Warn(ex, "Failed to load output thumbnail for queue item {Id}", item.Id)
                );
        }
    }

    /// <summary>
    /// Decodes a thumbnail of the item's first output image (off the UI thread) and assigns it
    /// as the card preview. Best-effort: missing or unreadable files are skipped. The decoded
    /// bitmap is queue-owned, so the usual dispose-on-replace/remove rules apply.
    /// </summary>
    private async Task LoadOutputThumbnailAsync(PromptQueueItem item)
    {
        var path = item.OutputImagePaths.FirstOrDefault();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        var thumbnail = await Task.Run(() =>
            {
                using var stream = File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, 256);
            })
            .ConfigureAwait(false);

        await Dispatcher
            .UIThread.InvokeAsync(() =>
            {
                // The item may have been removed or re-queued while decoding - don't leak the
                // bitmap or attach a stale thumbnail to a pending item
                if (!Items.Contains(item) || item.Status != PromptQueueItemStatus.Completed)
                {
                    thumbnail.Dispose();
                    return;
                }

                var previous = item.PreviewImage;
                item.PreviewImage = thumbnail;
                previous?.Dispose();
            })
            .GetTask()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a human-readable error message from a Comfy node execution error,
    /// e.g. "KSampler: mat1 and mat2 shapes cannot be multiplied".
    /// </summary>
    private static string FormatNodeError(ComfyNodeException e)
    {
        var errorData = e.ErrorData;

        if (string.IsNullOrEmpty(errorData.ExceptionMessage))
        {
            return "Node execution encountered an error";
        }

        return string.IsNullOrEmpty(errorData.NodeType)
            ? errorData.ExceptionMessage
            : $"{errorData.NodeType}: {errorData.ExceptionMessage}";
    }

    private void SetFailed(PromptQueueItem item, string? message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            item.Status = PromptQueueItemStatus.Failed;
            item.ErrorMessage = message;
            item.CompletedAt = DateTimeOffset.Now;
            item.ProgressText = null;
        });
    }

    private static void CaptureOutputs(
        InferenceGenerationViewModelBase generationViewModel,
        PromptQueueItem item
    )
    {
        var paths = generationViewModel
            .ImageGalleryCardViewModel.ImageSources.Select(source => source.LocalFile?.FullPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .ToList();

        if (paths.Count > 0)
        {
            item.OutputImagePaths = paths;
        }
    }

    /// <summary>
    /// Mirrors a running headless generation view model's live progress and preview image onto
    /// the queue item so the queue UI shows the same progress + thumbnail as a normal tab.
    /// </summary>
    private sealed class ProgressMirror : IDisposable
    {
        private readonly InferenceGenerationViewModelBase generationViewModel;
        private readonly PromptQueueItem item;

        // Posted lambdas may run after the headless view model's scope is disposed, so they must
        // only touch the item and values captured at event time - never the view model.
        private volatile bool isDisposed;

        public ProgressMirror(InferenceGenerationViewModelBase generationViewModel, PromptQueueItem item)
        {
            this.generationViewModel = generationViewModel;
            this.item = item;

            generationViewModel.OutputProgress.PropertyChanged += OnProgressChanged;
            generationViewModel.ImageGalleryCardViewModel.PropertyChanged += OnGalleryChanged;
        }

        private void OnProgressChanged(object? sender, PropertyChangedEventArgs e)
        {
            var progress = generationViewModel.OutputProgress;
            var value = progress.Value;
            var maximum = progress.Maximum;
            var text = progress.Text;

            Dispatcher.UIThread.Post(() =>
            {
                if (isDisposed)
                    return;

                item.Progress = maximum > 0 ? value / maximum * 100 : value;
                item.ProgressText = text;
            });
        }

        private void OnGalleryChanged(object? sender, PropertyChangedEventArgs e)
        {
            var gallery = generationViewModel.ImageGalleryCardViewModel;

            if (e.PropertyName != nameof(gallery.PreviewImage))
                return;

            var preview = gallery.PreviewImage;

            Dispatcher.UIThread.Post(() =>
            {
                if (isDisposed)
                    return;

                var previous = item.PreviewImage;
                item.PreviewImage = preview;

                // The item shares bitmap instances with the gallery: the incoming preview is the
                // gallery's current bitmap, so only the replaced (no longer referenced) one may
                // be disposed.
                if (previous is not null && !ReferenceEquals(previous, preview))
                {
                    previous.Dispose();
                }
            });
        }

        public void Dispose()
        {
            isDisposed = true;
            generationViewModel.OutputProgress.PropertyChanged -= OnProgressChanged;
            generationViewModel.ImageGalleryCardViewModel.PropertyChanged -= OnGalleryChanged;
        }
    }

    #endregion

    #region Persistence

    private void AttachItemHandlers(PromptQueueItem item)
    {
        item.PropertyChanged += OnItemPropertyChanged;
    }

    private void DetachItemHandlers(PromptQueueItem item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Preview and progress churn every frame while running; don't persist those.
        if (
            e.PropertyName
            is nameof(PromptQueueItem.PreviewImage)
                or nameof(PromptQueueItem.Progress)
                or nameof(PromptQueueItem.ProgressText)
        )
        {
            return;
        }

        RequestSave();
    }

    private async Task LoadAsync()
    {
        try
        {
            var file = QueueFile;
            if (!file.Exists)
            {
                isLoaded = true;
                return;
            }

            // Read and deserialize off the UI thread; RegisterOnLibraryDirSet callbacks run
            // synchronously on the UI thread during startup.
            var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<List<PromptQueueItem>>(json, SerializerOptions);

            await Dispatcher
                .UIThread.InvokeAsync(() =>
                {
                    if (loaded is not null)
                    {
                        foreach (var item in loaded)
                        {
                            // Items that were mid-run when the app closed become pending again.
                            if (item.Status == PromptQueueItemStatus.Running)
                            {
                                item.Status = PromptQueueItemStatus.Pending;
                                item.Progress = 0;
                                item.ProgressText = null;
                                item.StartedAt = null;
                            }

                            AttachItemHandlers(item);
                            Items.Add(item);
                        }
                    }

                    isLoaded = true;
                })
                .GetTask()
                .ConfigureAwait(false);

            // Lazily restore output thumbnails for completed items (best-effort, background)
            foreach (
                var item in (loaded ?? []).Where(x =>
                    x.Status == PromptQueueItemStatus.Completed && x.OutputImagePaths.Count > 0
                )
            )
            {
                LoadOutputThumbnailAsync(item)
                    .SafeFireAndForget(ex =>
                        Logger.Warn(ex, "Failed to load output thumbnail for queue item {Id}", item.Id)
                    );
            }
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to load prompt queue");
            isLoaded = true;
        }
    }

    private void RequestSave()
    {
        if (!isLoaded || !settingsManager.IsLibraryDirSet)
            return;

        savePending = true;

        var cts = new CancellationTokenSource();
        // Read the token before spawning: a subsequent RequestSave or shutdown may cancel the
        // CTS while the delayed task runs, and the closure must never touch the CTS itself.
        var token = cts.Token;

        var old = Interlocked.Exchange(ref delayedSaveCts, cts);
        try
        {
            // Cancel and drop the old CTS; it is left for the GC rather than disposed so any
            // in-flight delayed task can't observe a disposed instance.
            old?.Cancel();
        }
        catch (ObjectDisposedException) { }

        Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
                        savePending = false;
                        await SaveAsync().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                    catch (ObjectDisposedException e)
                    {
                        Logger.Debug(e, "Prompt queue save debounce raced with disposal");
                    }
                },
                CancellationToken.None
            )
            .SafeFireAndForget();
    }

    private async Task SaveAsync()
    {
        // Snapshot on the UI thread since Items is UI-owned.
        var snapshot = await Dispatcher
            .UIThread.InvokeAsync(() => Items.ToList())
            .GetTask()
            .ConfigureAwait(false);

        WriteSnapshot(snapshot);
    }

    private void WriteSnapshot(List<PromptQueueItem> snapshot)
    {
        lock (saveLock)
        {
            try
            {
                var file = QueueFile;
                file.Directory?.Create();

                var json = JsonSerializer.Serialize(snapshot, SerializerOptions);

                var tempPath = file + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, file, overwrite: true);
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to save prompt queue");
            }
        }
    }

    /// <summary>
    /// Flushes any pending debounced save on shutdown so closing the app within the debounce
    /// window doesn't drop changes. Called when the service provider is disposed on exit.
    /// </summary>
    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref delayedSaveCts, null);
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException) { }

        if (savePending && settingsManager.IsLibraryDirSet)
        {
            savePending = false;

            try
            {
                var snapshot = Dispatcher.UIThread.CheckAccess()
                    ? Items.ToList()
                    : Dispatcher.UIThread.Invoke(() => Items.ToList());

                WriteSnapshot(snapshot);
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to flush prompt queue on shutdown");
            }
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
