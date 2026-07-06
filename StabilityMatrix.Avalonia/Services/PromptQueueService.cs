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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<IPromptQueueService, PromptQueueService>]
public partial class PromptQueueService : ObservableObject, IPromptQueueService
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

    private bool isLoaded;

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

        settingsManager.RegisterOnLibraryDirSet(_ => Load());

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
            CancelCurrent();
        }

        DetachItemHandlers(item);
        Items.Remove(item);
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
            DetachItemHandlers(item);
            Items.Remove(item);
        }
    }

    public void ClearPending()
    {
        foreach (var item in Items.Where(x => x.Status == PromptQueueItemStatus.Pending).ToList())
        {
            DetachItemHandlers(item);
            Items.Remove(item);
        }
    }

    private static bool IsFinished(PromptQueueItem item) =>
        item.Status
            is PromptQueueItemStatus.Completed
                or PromptQueueItemStatus.Failed
                or PromptQueueItemStatus.Cancelled;

    #region Processing

    public void StartProcessing()
    {
        if (IsProcessing)
            return;

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
        currentItemCts?.Cancel();
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

                // Require a connected client. If not connected, notify once, leave the item
                // pending, and stop processing.
                if (!clientManager.IsConnected)
                {
                    notificationService.Show(
                        Resources.Label_PromptQueue,
                        Resources.Label_PromptQueueNotConnected,
                        NotificationType.Warning
                    );
                    break;
                }

                await ProcessItemAsync(item).ConfigureAwait(false);
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
        using var scope = vmFactory.CreateScope();

        if (item.ProjectType.ToViewModelType() is not { } vmType)
        {
            SetFailed(item, $"Unsupported project type: {item.ProjectType}");
            return;
        }

        if (scope.ServiceManager.Get(vmType) is not InferenceGenerationViewModelBase generationViewModel)
        {
            SetFailed(item, $"Could not create view model for {item.ProjectType}");
            return;
        }

        using var cts = new CancellationTokenSource();
        currentItemCts = cts;

        await Dispatcher
            .UIThread.InvokeAsync(() =>
            {
                if (item.Project.State is { } state)
                {
                    generationViewModel.LoadStateFromJsonObject(state);
                }

                item.Status = PromptQueueItemStatus.Running;
                item.StartedAt = DateTimeOffset.Now;
                item.Progress = 0;
                item.ProgressText = null;
                item.ErrorMessage = null;
                CurrentItem = item;
            })
            .GetTask()
            .ConfigureAwait(false);

        var progressMirror = new ProgressMirror(generationViewModel, item);

        try
        {
            await generationViewModel.RunGenerationForQueueAsync(cts.Token).ConfigureAwait(false);

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
                    item.PreviewImage = null;
                    if (CurrentItem == item)
                        CurrentItem = null;
                })
                .GetTask()
                .ConfigureAwait(false);
        }
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
            // Marshal to UI thread; progress events already post there but stay defensive.
            Dispatcher.UIThread.Post(() =>
            {
                item.Progress =
                    progress.Maximum > 0 ? progress.Value / progress.Maximum * 100 : progress.Value;
                item.ProgressText = progress.Text;
            });
        }

        private void OnGalleryChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(generationViewModel.ImageGalleryCardViewModel.PreviewImage))
                return;

            var preview = generationViewModel.ImageGalleryCardViewModel.PreviewImage;
            Dispatcher.UIThread.Post(() => item.PreviewImage = preview);
        }

        public void Dispose()
        {
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

    private void Load()
    {
        try
        {
            var file = QueueFile;
            if (!file.Exists)
            {
                isLoaded = true;
                return;
            }

            var json = File.ReadAllText(file);
            var loaded = JsonSerializer.Deserialize<List<PromptQueueItem>>(json, SerializerOptions);

            Dispatcher.UIThread.Post(() =>
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
            });
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

        var cts = new CancellationTokenSource();
        var old = Interlocked.Exchange(ref delayedSaveCts, cts);
        try
        {
            old?.Cancel();
            old?.Dispose();
        }
        catch (ObjectDisposedException) { }

        Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token).ConfigureAwait(false);
                        Save();
                    }
                    catch (TaskCanceledException) { }
                    finally
                    {
                        cts.Dispose();
                    }
                },
                CancellationToken.None
            )
            .SafeFireAndForget();
    }

    private void Save()
    {
        // Snapshot on the UI thread since Items is UI-owned.
        List<PromptQueueItem> snapshot = null!;
        Dispatcher.UIThread.Invoke(() => snapshot = Items.ToList());

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

    #endregion
}
