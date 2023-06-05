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

public class CheckpointFolder : ObservableObject
{
    /// <summary>
    /// Absolute path to the folder.
    /// </summary>
    public string DirectoryPath { get; init; } = string.Empty;
    
    /// <summary>
    /// Custom title for UI.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isCurrentDragTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDragBlurEnabled))]
    private bool isImportInProgress;

    public bool IsDragBlurEnabled => IsCurrentDragTarget || IsImportInProgress;
    
    public ProgressViewModel Progress { get; } = new();

    public ObservableCollection<CheckpointFile> CheckpointFiles { get; set; } = new();
    
    public RelayCommand OnPreviewDragEnterCommand => new(() => IsCurrentDragTarget = true);
    public RelayCommand OnPreviewDragLeaveCommand => new(() => IsCurrentDragTarget = false);

    public CheckpointFolder()
    {
        CheckpointFiles.CollectionChanged += OnCheckpointFilesChanged;
    }
    
    // On collection changes
    private void OnCheckpointFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
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
        
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length < 1)
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
            null => Task.Run(() => CheckpointFile.FromDirectoryIndex(DirectoryPath)),
            _ => Task.Run(() => CheckpointFile.FromDirectoryIndex(DirectoryPath, progress))
        });

        CheckpointFiles.Clear();
        foreach (var checkpointFile in checkpointFiles)
        {
            CheckpointFiles.Add(checkpointFile);
        }
    }
}
