using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointManager;

[ManagedService]
[Transient]
public partial class CheckpointFolder : ViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;
    private readonly ModelFinder modelFinder;
    private readonly INotificationService notificationService;

    public SourceCache<CheckpointFolder, string> SubFoldersCache { get; } =
        new(x => x.DirectoryPath);

    private readonly SourceCache<CheckpointFile, string> checkpointFilesCache =
        new(x => x.FilePath);

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private bool useCategoryVisibility;

    /// <summary>
    /// Absolute path to the folder.
    /// </summary>
    public string DirectoryPath { get; init; } = string.Empty;

    /// <summary>
    /// Custom title for UI.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderType))]
    [NotifyPropertyChangedFor(nameof(TitleWithFilesCount))]
    private string title = string.Empty;

    [ObservableProperty]
    private SharedFolderType folderType;

    /// <summary>
    /// True if the category is enabled for the manager page.
    /// </summary>
    [ObservableProperty]
    private bool isCategoryEnabled = true;

    /// <summary>
    /// True if currently expanded in the UI.
    /// </summary>
    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isCurrentDragTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isImportInProgress;

    [ObservableProperty]
    private string searchFilter = string.Empty;

    public bool IsDragBlurEnabled => IsCurrentDragTarget || IsImportInProgress;

    public string TitleWithFilesCount =>
        CheckpointFiles.Any() || SubFolders.Any(f => f.CheckpointFiles.Any())
            ? $"{FolderType.GetDescription() ?? FolderType.GetStringValue()} ({CheckpointFiles.Count + SubFolders.Sum(folder => folder.CheckpointFiles.Count)})"
            : FolderType.GetDescription() ?? FolderType.GetStringValue();

    public ProgressViewModel Progress { get; } = new();

    public CheckpointFolder? ParentFolder { get; init; }

    public IObservableCollection<CheckpointFolder> SubFolders { get; } =
        new ObservableCollectionExtended<CheckpointFolder>();

    public IObservableCollection<CheckpointFile> CheckpointFiles { get; } =
        new ObservableCollectionExtended<CheckpointFile>();

    public IObservableCollection<CheckpointFile> DisplayedCheckpointFiles { get; set; } =
        new ObservableCollectionExtended<CheckpointFile>();

    public CheckpointFolder(
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        ModelFinder modelFinder,
        INotificationService notificationService,
        bool useCategoryVisibility = true
    )
    {
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
        this.modelFinder = modelFinder;
        this.notificationService = notificationService;
        this.useCategoryVisibility = useCategoryVisibility;

        checkpointFilesCache
            .Connect()
            .DeferUntilLoaded()
            .SubscribeMany(
                file =>
                    Observable
                        .FromEventPattern<EventArgs>(file, nameof(ParentListRemoveRequested))
                        .Subscribe(_ => checkpointFilesCache.Remove(file))
            )
            .Bind(CheckpointFiles)
            .Sort(
                SortExpressionComparer<CheckpointFile>
                    .Descending(f => f.IsConnectedModel)
                    .ThenByAscending(
                        f => f.IsConnectedModel ? f.ConnectedModel!.ModelName : f.FileName
                    )
            )
            .Filter(
                f =>
                    f.FileName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                    || f.Title.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
            )
            .Bind(DisplayedCheckpointFiles)
            .Subscribe();

        SubFoldersCache
            .Connect()
            .DeferUntilLoaded()
            .SortBy(x => x.Title)
            .Bind(SubFolders)
            .Subscribe();

        CheckpointFiles.CollectionChanged += OnCheckpointFilesChanged;
        // DisplayedCheckpointFiles = CheckpointFiles;
    }

    /// <summary>
    /// When title is set, set the category enabled state from settings.
    /// </summary>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnTitleChanged(string value)
    {
        if (!useCategoryVisibility)
            return;

        // Update folder type
        var result = Enum.TryParse(Title, out SharedFolderType type);
        FolderType = result ? type : new SharedFolderType();

        IsCategoryEnabled = settingsManager.IsSharedFolderCategoryVisible(FolderType);
    }

    partial void OnSearchFilterChanged(string value)
    {
        foreach (var subFolder in SubFolders)
        {
            subFolder.SearchFilter = value;
        }

        checkpointFilesCache.Refresh();
    }

    /// <summary>
    /// When toggling the category enabled state, save it to settings.
    /// </summary>
    partial void OnIsCategoryEnabledChanged(bool value)
    {
        if (!useCategoryVisibility)
            return;
        if (value != settingsManager.IsSharedFolderCategoryVisible(FolderType))
        {
            settingsManager.SetSharedFolderCategoryVisible(FolderType, value);
        }
    }

    private void OnCheckpointFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TitleWithFilesCount));
    }

    public async Task OnDrop(DragEventArgs e)
    {
        IsImportInProgress = true;
        IsCurrentDragTarget = false;

        try
        {
            // {System.Linq.Enumerable.WhereEnumerableIterator<Avalonia.Platform.Storage.IStorageItem>}
            if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
            {
                var paths = files.Select(f => f.Path.LocalPath).ToArray();
                await ImportFilesAsync(paths, settingsManager.Settings.IsImportAsConnected);
            }
            else if (e.Data.Get("Context") is CheckpointFile file)
            {
                await MoveBetweenFolders(file);
            }
        }
        catch (Exception)
        {
            // If no exception this will be handled by DelayedClearProgress()
            IsImportInProgress = false;
        }
    }

    [RelayCommand]
    private async Task ShowInExplorer(string path)
    {
        await ProcessRunner.OpenFolderBrowser(path);
    }

    [RelayCommand]
    private async Task Delete()
    {
        var directory = new DirectoryPath(DirectoryPath);

        if (!directory.Exists)
        {
            RemoveFromParentList();
            return;
        }

        var dialog = DialogHelper.CreateTaskDialog(
            "Are you sure you want to delete this folder?",
            directory
        );

        dialog.ShowProgressBar = false;
        dialog.Buttons = new List<TaskDialogButton>
        {
            TaskDialogButton.YesButton,
            TaskDialogButton.NoButton
        };

        dialog.Closing += async (sender, e) =>
        {
            // We only want to use the deferral on the 'Yes' Button
            if ((TaskDialogStandardResult)e.Result == TaskDialogStandardResult.Yes)
            {
                var deferral = e.GetDeferral();

                sender.ShowProgressBar = true;
                sender.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);

                await using (new MinimumDelay(200, 300))
                {
                    await directory.DeleteAsync(true);
                }

                RemoveFromParentList();
                deferral.Complete();
            }
        };

        dialog.XamlRoot = App.VisualRoot;

        await dialog.ShowAsync(true);
    }

    [RelayCommand]
    private async Task CreateSubFolder()
    {
        Dispatcher.UIThread.VerifyAccess();

        var textBox = new TextBox();
        var dialog = new ContentDialog
        {
            Title = "Folder name",
            Content = textBox,
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = true,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var targetName = textBox.Text;
            if (string.IsNullOrWhiteSpace(targetName))
                return;

            var subFolderPath = Path.Combine(DirectoryPath, targetName);

            Directory.CreateDirectory(subFolderPath);

            SubFolders.Add(
                new CheckpointFolder(
                    settingsManager,
                    downloadService,
                    modelFinder,
                    notificationService,
                    useCategoryVisibility: false
                )
                {
                    Title = Path.GetFileName(subFolderPath),
                    DirectoryPath = subFolderPath,
                    FolderType = FolderType,
                    ParentFolder = this,
                    IsExpanded = false,
                }
            );
        }
    }

    public async Task MoveBetweenFolders(CheckpointFile sourceFile)
    {
        var delay = 1.5f;
        try
        {
            Progress.Value = 0;
            var sourcePath = new FilePath(sourceFile.FilePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
            var sourceCmInfoPath = Path.Combine(
                sourcePath.Directory,
                $"{fileNameWithoutExt}.cm-info.json"
            );
            var sourcePreviewPath = Path.Combine(
                sourcePath.Directory,
                $"{fileNameWithoutExt}.preview.jpeg"
            );
            var destinationFilePath = Path.Combine(DirectoryPath, sourcePath.Name);
            var destinationCmInfoPath = Path.Combine(
                DirectoryPath,
                $"{fileNameWithoutExt}.cm-info.json"
            );
            var destinationPreviewPath = Path.Combine(
                DirectoryPath,
                $"{fileNameWithoutExt}.preview.jpeg"
            );

            // Move files
            if (File.Exists(sourcePath))
            {
                Progress.Text = $"Moving {sourcePath.Name}...";
                await FileTransfers.MoveFileAsync(sourcePath, destinationFilePath);
            }

            Progress.Value = 33;
            Progress.Text = $"Moving {sourcePath.Name} metadata...";

            if (File.Exists(sourceCmInfoPath))
            {
                await FileTransfers.MoveFileAsync(sourceCmInfoPath, destinationCmInfoPath);
            }

            Progress.Value = 66;

            if (File.Exists(sourcePreviewPath))
            {
                await FileTransfers.MoveFileAsync(sourcePreviewPath, destinationPreviewPath);
            }

            Progress.Value = 100;
            Progress.Text = $"Moved {sourcePath.Name} to {Title}";
            sourceFile.OnMoved();
            BackgroundIndex();
            delay = 0.5f;
        }
        catch (FileTransferExistsException)
        {
            Progress.Value = 0;
            Progress.Text = "Failed to move file: destination file exists";
        }
        finally
        {
            DelayedClearProgress(TimeSpan.FromSeconds(delay));
        }
    }

    /// <summary>
    /// Imports files to the folder. Reports progress to instance properties.
    /// </summary>
    public async Task ImportFilesAsync(
        IEnumerable<string> files,
        bool convertToConnected = false,
        bool copyFiles = true
    )
    {
        try
        {
            Progress.Value = 0;
            var copyPaths = files.ToDictionary(
                k => k,
                v => Path.Combine(DirectoryPath, Path.GetFileName(v))
            );

            var progress = new Progress<ProgressReport>(report =>
            {
                Progress.IsIndeterminate = false;
                Progress.Value = report.Percentage;
                // For multiple files, add count
                Progress.Text =
                    copyPaths.Count > 1
                        ? $"Importing {report.Title} ({report.Message})"
                        : $"Importing {report.Title}";
            });

            if (copyFiles)
            {
                await FileTransfers.CopyFiles(copyPaths, progress);
            }

            // Hash files and convert them to connected model if found
            if (convertToConnected)
            {
                var modelFilesCount = copyPaths.Count;
                var modelFiles = copyPaths.Values.Select(path => new FilePath(path));

                // Holds tasks for model queries after hash
                var modelQueryTasks = new List<Task<bool>>();

                foreach (var (i, modelFile) in modelFiles.Enumerate())
                {
                    var hashProgress = new Progress<ProgressReport>(report =>
                    {
                        Progress.IsIndeterminate = report.IsIndeterminate;
                        Progress.Value = report.Percentage;
                        Progress.Text =
                            modelFilesCount > 1
                                ? $"Computing metadata for {modelFile.Name} ({i}/{modelFilesCount})"
                                : $"Computing metadata for {modelFile.Name}";
                    });

                    var hashBlake3 = await FileHash.GetBlake3Async(modelFile, hashProgress);

                    // Start a task to query the model in background
                    var queryTask = Task.Run(async () =>
                    {
                        var result = await modelFinder.LocalFindModel(hashBlake3);
                        result ??= await modelFinder.RemoteFindModel(hashBlake3);

                        if (result is null)
                            return false; // Not found

                        var (model, version, file) = result.Value;

                        // Save connected model info json
                        var modelFileName = Path.GetFileNameWithoutExtension(modelFile.Info.Name);
                        var modelInfo = new ConnectedModelInfo(
                            model,
                            version,
                            file,
                            DateTimeOffset.UtcNow
                        );
                        await modelInfo.SaveJsonToDirectory(DirectoryPath, modelFileName);

                        // If available, save thumbnail
                        var image = version.Images?.FirstOrDefault();
                        if (image != null)
                        {
                            var imageExt = Path.GetExtension(image.Url).TrimStart('.');
                            if (imageExt is "jpg" or "jpeg" or "png")
                            {
                                var imageDownloadPath = Path.GetFullPath(
                                    Path.Combine(
                                        DirectoryPath,
                                        $"{modelFileName}.preview.{imageExt}"
                                    )
                                );
                                await downloadService.DownloadToFileAsync(
                                    image.Url,
                                    imageDownloadPath
                                );
                            }
                        }

                        return true;
                    });
                    modelQueryTasks.Add(queryTask);
                }

                // Set progress to indeterminate
                Progress.IsIndeterminate = true;
                Progress.Text = "Checking connected model information";

                // Wait for all model queries to finish
                var modelQueryResults = await Task.WhenAll(modelQueryTasks);

                var successCount = modelQueryResults.Count(r => r);
                var totalCount = modelQueryResults.Length;
                var failCount = totalCount - successCount;

                BackgroundIndex();

                Progress.Value = 100;
                Progress.Text = successCount switch
                {
                    0 when failCount > 0 => "Import complete. No connected data found.",
                    > 0 when failCount > 0
                        => $"Import complete. Found connected data for {successCount} of {totalCount} models.",
                    1 when failCount == 0 => "Import complete. Found connected data for 1 model.",
                    _ => $"Import complete. Found connected data for all {totalCount} models."
                };
            }
            else
            {
                Progress.Text = "Import complete";
                Progress.Value = 100;
                BackgroundIndex();
            }
        }
        finally
        {
            DelayedClearProgress(TimeSpan.FromSeconds(1.5));
        }
    }

    public async Task FindConnectedMetadata()
    {
        IsImportInProgress = true;
        var files = CheckpointFiles
            .Where(f => !f.IsConnectedModel)
            .Select(f => f.FilePath)
            .ToList();

        if (files.Any())
        {
            await ImportFilesAsync(files, true, false);
        }
        else
        {
            notificationService.Show(
                "Cannot Find Connected Metadata",
                "All files in this folder are already connected.",
                NotificationType.Warning
            );
        }
    }

    /// <summary>
    /// Clears progress after a delay.
    /// </summary>
    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay)
            .ContinueWith(_ =>
            {
                IsImportInProgress = false;
                Progress.Value = 0;
                Progress.IsIndeterminate = false;
                Progress.Text = string.Empty;
            });
    }

    /// <summary>
    /// Gets checkpoint files from folder index
    /// </summary>
    private IEnumerable<CheckpointFile> GetCheckpointFiles()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return Enumerable.Empty<CheckpointFile>();
        }

        return CheckpointFile.FromDirectoryIndex(this, DirectoryPath);
    }

    /// <summary>
    /// Indexes the folder for checkpoint files and refreshes the CheckPointFiles collection.
    /// </summary>
    public void BackgroundIndex()
    {
        Dispatcher.UIThread.Post(Index, DispatcherPriority.Background);
    }

    /// <summary>
    /// Indexes the folder for checkpoint files and refreshes the CheckPointFiles collection.
    /// </summary>
    public void Index()
    {
        // Get subfolders
        foreach (var folder in Directory.GetDirectories(DirectoryPath))
        {
            // Get from cache or create new
            if (SubFoldersCache.Lookup(folder) is { HasValue: true } result)
            {
                result.Value.BackgroundIndex();
            }
            else
            {
                // Create subfolder
                var subFolder = new CheckpointFolder(
                    settingsManager,
                    downloadService,
                    modelFinder,
                    notificationService,
                    useCategoryVisibility: false
                )
                {
                    Title = Path.GetFileName(folder),
                    DirectoryPath = folder,
                    FolderType = FolderType, // Inherit our folder type
                    ParentFolder = this,
                    IsExpanded = false, // Subfolders are collapsed by default
                };
                subFolder.BackgroundIndex();
                SubFoldersCache.AddOrUpdate(subFolder);
            }
        }

        // Index files
        Dispatcher.UIThread.Post(
            () =>
            {
                var files = GetCheckpointFiles();
                checkpointFilesCache.EditDiff(files, CheckpointFile.FilePathComparer);
            },
            DispatcherPriority.Background
        );
    }
}
