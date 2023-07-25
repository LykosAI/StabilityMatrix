using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class CheckpointFolder : ViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;
    private readonly ModelFinder modelFinder;
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
    [ObservableProperty] private bool isCategoryEnabled = true;

    /// <summary>
    /// True if currently expanded in the UI.
    /// </summary>
    [ObservableProperty] private bool isExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isCurrentDragTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isImportInProgress;

    [ObservableProperty] private string searchFilter = string.Empty;
    
    public bool IsDragBlurEnabled => IsCurrentDragTarget || IsImportInProgress;

    public string TitleWithFilesCount =>
        CheckpointFiles.Any() || SubFolders.Any(f => f.CheckpointFiles.Any())
            ? $"{Title} ({CheckpointFiles.Count + SubFolders.Sum(folder => folder.CheckpointFiles.Count)})"
            : Title;
    
    public ProgressViewModel Progress { get; } = new();

    public CheckpointFolder? ParentFolder { get; init; }
    public ObservableCollection<CheckpointFolder> SubFolders { get; init; } = new();
    public ObservableCollection<CheckpointFile> CheckpointFiles { get; init; } = new();
    public ObservableCollection<CheckpointFile> DisplayedCheckpointFiles { get; set; }

    public CheckpointFolder(
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        ModelFinder modelFinder,
        bool useCategoryVisibility = true)
    {
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
        this.modelFinder = modelFinder;
        this.useCategoryVisibility = useCategoryVisibility;
        
        CheckpointFiles.CollectionChanged += OnCheckpointFilesChanged;
        DisplayedCheckpointFiles = CheckpointFiles;
    }
    
    /// <summary>
    /// When title is set, set the category enabled state from settings.
    /// </summary>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnTitleChanged(string value)
    {
        if (!useCategoryVisibility) return;
        
        // Update folder type
        var result = Enum.TryParse(Title, out SharedFolderType type);
        FolderType = result ? type : new SharedFolderType();
        
        IsCategoryEnabled = settingsManager.IsSharedFolderCategoryVisible(FolderType);
    }

    partial void OnSearchFilterChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            DisplayedCheckpointFiles = CheckpointFiles;
        }
        else
        {
            var filteredFiles = CheckpointFiles.Where(y =>
                y.FileName.Contains(value, StringComparison.OrdinalIgnoreCase));
            DisplayedCheckpointFiles = new ObservableCollection<CheckpointFile>(filteredFiles);
        }
    }

    /// <summary>
    /// When toggling the category enabled state, save it to settings.
    /// </summary>
    partial void OnIsCategoryEnabledChanged(bool value)
    {
        if (!useCategoryVisibility) return;
        if (value != settingsManager.IsSharedFolderCategoryVisible(FolderType))
        {
            settingsManager.SetSharedFolderCategoryVisible(FolderType, value);
        }
    }

    // On collection changes
    private void OnCheckpointFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TitleWithFilesCount));
        if (e.NewItems == null) return;
        // On new added items, add event handler for deletion
        foreach (CheckpointFile item in e.NewItems)
        {
            item.Deleted += OnCheckpointFileDelete;
        }
    }

    /// <summary>
    /// Handler for CheckpointFile requesting to be deleted from the collection.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="file"></param>
    private void OnCheckpointFileDelete(object? sender, CheckpointFile file)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CheckpointFiles.Remove(file);
            DisplayedCheckpointFiles.Remove(file);
        });
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
    private async Task CreateSubFolder()
    {
        Dispatcher.UIThread.VerifyAccess();
        
        var textBox = new TextBox();
        var dialog = new ContentDialog
        {
            Content = textBox,
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = "OK",
            IsPrimaryButtonEnabled = true,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var targetName = textBox.Text;
            if (string.IsNullOrWhiteSpace(targetName)) return;
            
            var subFolderPath = Path.Combine(DirectoryPath, targetName);
            
            Directory.CreateDirectory(subFolderPath);
            
            SubFolders.Add(new CheckpointFolder(settingsManager, 
                downloadService, modelFinder,
                useCategoryVisibility: false)
            {
                Title = Path.GetFileName(subFolderPath),
                DirectoryPath = subFolderPath,
                FolderType = FolderType,
                ParentFolder = this,
                IsExpanded = false,
            });
        }
    }
    
    /// <summary>
    /// Imports files to the folder. Reports progress to instance properties.
    /// </summary>
    public async Task ImportFilesAsync(IEnumerable<string> files, bool convertToConnected = false)
    {
        Progress.Value = 0;
        var copyPaths = files.ToDictionary(k => k, v => Path.Combine(DirectoryPath, Path.GetFileName(v)));
        
        var progress = new Progress<ProgressReport>(report =>
        {
            Progress.IsIndeterminate = false;
            Progress.Value = report.Percentage;
            // For multiple files, add count
            Progress.Text = copyPaths.Count > 1 ? $"Importing {report.Title} ({report.Message})" : $"Importing {report.Title}";
        });

        await FileTransfers.CopyFiles(copyPaths, progress);
        
        // Hash files and convert them to connected model if found
        if (convertToConnected)
        {
            var modelFilesCount = copyPaths.Count;
            var modelFiles = copyPaths.Values
                .Select(path => new FilePath(path));
            
            // Holds tasks for model queries after hash
            var modelQueryTasks = new List<Task<bool>>();

            foreach (var (i, modelFile) in modelFiles.Enumerate())
            {
                var hashProgress = new Progress<ProgressReport>(report =>
                {
                    Progress.IsIndeterminate = false;
                    Progress.Value = report.Percentage;
                    Progress.Text = modelFilesCount > 1 ? 
                        $"Computing metadata for {modelFile.Info.Name} ({i}/{modelFilesCount})" : 
                        $"Computing metadata for {report.Title}";
                });
                
                var hashBlake3 = await FileHash.GetBlake3Async(modelFile, hashProgress);
                
                // Start a task to query the model in background
                var queryTask = Task.Run(async () =>
                {
                    var result = await modelFinder.LocalFindModel(hashBlake3);
                    result ??= await modelFinder.RemoteFindModel(hashBlake3);

                    if (result is null) return false; // Not found

                    var (model, version, file) = result.Value;
                    
                    // Save connected model info json
                    var modelFileName = Path.GetFileNameWithoutExtension(modelFile.Info.Name);
                    var modelInfo = new ConnectedModelInfo(
                        model, version, file, DateTimeOffset.UtcNow);
                    await modelInfo.SaveJsonToDirectory(DirectoryPath, modelFileName);

                    // If available, save thumbnail
                    var image = version.Images?.FirstOrDefault();
                    if (image != null)
                    {
                        var imageExt = Path.GetExtension(image.Url).TrimStart('.');
                        if (imageExt is "jpg" or "jpeg" or "png")
                        {
                            var imageDownloadPath = Path.GetFullPath(
                                Path.Combine(DirectoryPath, $"{modelFileName}.preview.{imageExt}"));
                            await downloadService.DownloadToFileAsync(image.Url, imageDownloadPath);
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

            await IndexAsync();
            
            Progress.Value = 100;
            Progress.Text = successCount switch
            {
                0 when failCount > 0 =>
                    "Import complete. No connected data found.",
                > 0 when failCount > 0 =>
                    $"Import complete. Found connected data for {successCount} of {totalCount} models.",
                _ => $"Import complete. Found connected data for all {totalCount} models."
            };
            
            DelayedClearProgress(TimeSpan.FromSeconds(1.5));
        }
        else
        {
            Progress.Text = "Import complete";
            Progress.Value = 100;
            await IndexAsync();
            DelayedClearProgress(TimeSpan.FromSeconds(1.5));
        }
    }

    /// <summary>
    /// Clears progress after a delay.
    /// </summary>
    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay).ContinueWith(_ =>
        {
            IsImportInProgress = false;
            Progress.Value = 0;
            Progress.Text = string.Empty;
        });
    }
    
    /// <summary>
    /// Gets checkpoint files from folder index
    /// </summary>
    private async Task<List<CheckpointFile>> GetCheckpointFilesAsync(IProgress<ProgressReport>? progress = default)
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return new List<CheckpointFile>();
        }

        return await (progress switch
        {
            null => Task.Run(() =>
                CheckpointFile.FromDirectoryIndex(DirectoryPath).ToList()),
            
            _ => Task.Run(() =>
                CheckpointFile.FromDirectoryIndex(DirectoryPath, progress).ToList())
        });
    }

    /// <summary>
    /// Indexes the folder for checkpoint files and refreshes the CheckPointFiles collection.
    /// </summary>
    public async Task IndexAsync(IProgress<ProgressReport>? progress = default)
    {
        SubFolders.Clear();
        // Get subfolders
        foreach (var folder in Directory.GetDirectories(DirectoryPath))
        {
            // Create subfolder
            var subFolder = new CheckpointFolder(settingsManager,
                downloadService, modelFinder,
                useCategoryVisibility: false)
            {
                Title = Path.GetFileName(folder),
                DirectoryPath = folder,
                FolderType = FolderType, // Inherit our folder type
                ParentFolder = this,
                IsExpanded = false, // Subfolders are collapsed by default
            };
            
            await subFolder.IndexAsync(progress);
            SubFolders.Add(subFolder);
        }
        
        CheckpointFiles.Clear();
        CheckpointFiles.AddRange(await GetCheckpointFilesAsync());
    }
}
