using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointManagerViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISharedFolders sharedFolders;
    private readonly ISettingsManager settingsManager;
    private readonly IDialogFactory dialogFactory;

    // Toggle button for auto hashing new drag-and-dropped files for connected upgrade
    [ObservableProperty] private bool isImportAsConnected;
    
    partial void OnIsImportAsConnectedChanged(bool value)
    {
        if (value != settingsManager.Settings.IsImportAsConnected)
        {
            settingsManager.SetIsImportAsConnected(value);
        }
    }
    
    public ObservableCollection<CheckpointFolder> CheckpointFolders { get; set; } = new();
    
    public CheckpointManagerViewModel(ISharedFolders sharedFolders, ISettingsManager settingsManager, IDialogFactory dialogFactory)
    {
        this.sharedFolders = sharedFolders;
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
    }
    
    public async Task OnLoaded()
    {
        // Set UI states
        IsImportAsConnected = settingsManager.Settings.IsImportAsConnected;
        
        var modelsDirectory = settingsManager.ModelsDirectory;
        // Get all folders within the shared folder root
        if (string.IsNullOrWhiteSpace(modelsDirectory))
        {
            return;
        }
        // Skip if the shared folder root doesn't exist
        if (!Directory.Exists(modelsDirectory))
        {
            Logger.Debug($"Skipped shared folder index - {modelsDirectory} doesn't exist");
            return;
        }
        var folders = Directory.GetDirectories(modelsDirectory);
        
        CheckpointFolders.Clear();

        // Results
        var indexedFolders = new ConcurrentBag<CheckpointFolder>();
        // Index all folders
        var tasks = folders.Select(f => Task.Run(async () =>
            {
                var checkpointFolder = new CheckpointFolder(dialogFactory, settingsManager)
                {
                    Title = Path.GetFileName(f), 
                    DirectoryPath = f
                };
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
