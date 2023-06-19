using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.Models;

public partial class CheckpointFolder : ObservableObject
{
    private readonly IDialogFactory dialogFactory;
    private readonly ISettingsManager settingsManager;
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

    private SharedFolderType FolderType => Enum.TryParse(Title, out SharedFolderType type)
        ? type
        : new SharedFolderType();

    /// <summary>
    /// True if the category is enabled for the manager page.
    /// </summary>
    [ObservableProperty]
    private bool isCategoryEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isCurrentDragTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isImportInProgress;
    
    public bool IsDragBlurEnabled => IsCurrentDragTarget || IsImportInProgress;
    public string TitleWithFilesCount => CheckpointFiles.Any() ? $"{Title} ({CheckpointFiles.Count})" : Title;
    
    public ProgressViewModel Progress { get; } = new();

    public ObservableCollection<CheckpointFile> CheckpointFiles { get; init; } = new();
    
    public RelayCommand OnPreviewDragEnterCommand => new(() => IsCurrentDragTarget = true);
    public RelayCommand OnPreviewDragLeaveCommand => new(() => IsCurrentDragTarget = false);

    public CheckpointFolder(IDialogFactory dialogFactory, ISettingsManager settingsManager, bool useCategoryVisibility = true)
    {
        this.dialogFactory = dialogFactory;
        this.settingsManager = settingsManager;
        this.useCategoryVisibility = useCategoryVisibility;
        CheckpointFiles.CollectionChanged += OnCheckpointFilesChanged;
    }
    
    /// <summary>
    /// When title is set, set the category enabled state from settings.
    /// </summary>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnTitleChanged(string value)
    {
        if (!useCategoryVisibility) return;
        IsCategoryEnabled = settingsManager.IsSharedFolderCategoryVisible(FolderType);
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
        Application.Current.Dispatcher.Invoke(() => CheckpointFiles.Remove(file));
    }

    [RelayCommand]
    private async Task OnPreviewDropAsync(DragEventArgs e)
    {
        IsImportInProgress = true;
        IsCurrentDragTarget = false;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length < 1)
        {
            IsImportInProgress = false;
            return;
        }

        await ImportFilesAsync(files);
    }
    
    /// <summary>
    /// Imports files to the folder. Reports progress to instance properties.
    /// </summary>
    public async Task ImportFilesAsync(IEnumerable<string> files)
    {
        Progress.IsIndeterminate = true;
        Progress.IsProgressVisible = true;
        var copyPaths = files.ToDictionary(k => k, v => Path.Combine(DirectoryPath, Path.GetFileName(v)));
        
        var progress = new Progress<ProgressReport>(report =>
        {
            Progress.IsIndeterminate = false;
            Progress.Value = report.Percentage;
            // For multiple files, add count
            Progress.Text = copyPaths.Count > 1 ? $"Importing {report.Title} ({report.Message})" : $"Importing {report.Title}";
        });

        await FileTransfers.CopyFiles(copyPaths, progress);
        Progress.Value = 100;
        Progress.Text = "Import complete";
        await IndexAsync();
        DelayedClearProgress(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Clears progress after a delay.
    /// </summary>
    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay).ContinueWith(_ =>
        {
            IsImportInProgress = false;
            Progress.IsProgressVisible = false;
            Progress.Value = 0;
            Progress.Text = string.Empty;
        });
    }

    /// <summary>
    /// Indexes the folder for checkpoint files.
    /// </summary>
    public async Task IndexAsync(IProgress<ProgressReport>? progress = default)
    {
        var checkpointFiles = await (progress switch
        {
            null => Task.Run(() => CheckpointFile.FromDirectoryIndex(dialogFactory, DirectoryPath)),
            _ => Task.Run(() => CheckpointFile.FromDirectoryIndex(dialogFactory, DirectoryPath, progress))
        });

        CheckpointFiles.Clear();
        foreach (var checkpointFile in checkpointFiles)
        {
            CheckpointFiles.Add(checkpointFile);
        }
    }
}
