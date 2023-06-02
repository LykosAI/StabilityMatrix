using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Models;

public partial class CheckpointFolder : ObservableObject
{
    /// <summary>
    /// Absolute path to the folder.
    /// </summary>
    [ObservableProperty]
    private string directoryPath;
    
    /// <summary>
    /// Custom title for UI.
    /// </summary>
    [ObservableProperty]
    private string title;
    
    /// <summary>
    /// State of indexing.
    /// </summary>
    [ObservableProperty]
    private LoadState indexState = LoadState.NotLoaded;

    public ObservableCollection<CheckpointFile> CheckpointFiles { get; set; } = new();

    /// <summary>
    /// Indexes the folder for checkpoint files.
    /// </summary>
    public async Task IndexAsync(IProgress<ProgressReport>? progress = default)
    {
        IndexState = LoadState.Loading;
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
        IndexState = LoadState.Loaded;
    }
}
