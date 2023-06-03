using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointManagerViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISharedFolders sharedFolders;
    public ObservableCollection<CheckpointFolder> CheckpointFolders { get; set; } = new();
    
    public CheckpointManagerViewModel(ISharedFolders sharedFolders)
    {
        this.sharedFolders = sharedFolders;
    }
    
    public async Task OnLoaded()
    {
        // Get all folders within the shared folder root
        if (string.IsNullOrWhiteSpace(sharedFolders.SharedFoldersPath))
        {
            return;
        }
        // Skip if the shared folder root doesn't exist
        if (!Directory.Exists(sharedFolders.SharedFoldersPath))
        {
            Logger.Debug($"Skipped shared folder index - {sharedFolders.SharedFoldersPath} doesn't exist");
            return;
        }
        var folders = Directory.GetDirectories(sharedFolders.SharedFoldersPath);
        
        CheckpointFolders.Clear();

        // Results
        var indexedFolders = new ConcurrentBag<CheckpointFolder>();
        // Index all folders
        var tasks = folders.Select(f => Task.Run(async () =>
            {
                var checkpointFolder = new CheckpointFolder {Title = Path.GetFileName(f), DirectoryPath = f};
                await checkpointFolder.IndexAsync();
                indexedFolders.Add(checkpointFolder);
            })).ToList();
        await Task.WhenAll(tasks);
        // Add to observable collection by alphabetical order
        foreach (var checkpointFolder in indexedFolders.OrderBy(f => f.Title))
        {
            CheckpointFolders.Add(checkpointFolder);
        }
    }

    public void OnFolderCardDrop()
    {
        
    }
}
