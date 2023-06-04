using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace StabilityMatrix.Models;

public class CheckpointFolder
{
    /// <summary>
    /// Absolute path to the folder.
    /// </summary>
    public string DirectoryPath { get; init; } = string.Empty;
    
    /// <summary>
    /// Custom title for UI.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    public ObservableCollection<CheckpointFile> CheckpointFiles { get; set; } = new();

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
