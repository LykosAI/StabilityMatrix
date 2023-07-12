using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(CheckpointsPage))]
public partial class CheckpointsPageViewModel : PageViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISharedFolders sharedFolders;
    private readonly ISettingsManager settingsManager;
    private readonly ModelFinder modelFinder;
    private readonly IDownloadService downloadService;
    
    public override string Title => "Checkpoints";
    public override Symbol Icon => Symbol.FolderLink;
    
    // Toggle button for auto hashing new drag-and-dropped files for connected upgrade
    [ObservableProperty] private bool isImportAsConnected;
    
    partial void OnIsImportAsConnectedChanged(bool value)
    {
        if (settingsManager.IsLibraryDirSet && 
            value != settingsManager.Settings.IsImportAsConnected)
        {
            settingsManager.Transaction(s => s.IsImportAsConnected = value);
        }
    }

    [ObservableProperty]
    private ObservableCollection<CheckpointFolder> checkpointFolders = new();
    
    public CheckpointsPageViewModel(
        ISharedFolders sharedFolders, 
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        ModelFinder modelFinder)
    {
        this.sharedFolders = sharedFolders;
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
        this.modelFinder = modelFinder;
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode) return;
        
        // Set UI states
        IsImportAsConnected = settingsManager.Settings.IsImportAsConnected;

        await IndexFolders();
    }

    private async Task IndexFolders()
    {
        var modelsDirectory = settingsManager.ModelsDirectory;
        // Get all folders within the shared folder root
        if (string.IsNullOrWhiteSpace(modelsDirectory))
        {
            CheckpointFolders.Clear();
            return;
        }
        // Skip if the shared folder root doesn't exist
        if (!Directory.Exists(modelsDirectory))
        {
            Logger.Debug($"Skipped shared folder index - {modelsDirectory} doesn't exist");
            CheckpointFolders.Clear();
            return;
        }
        var folders = Directory.GetDirectories(modelsDirectory);
        
        // Index all folders
        var indexTasks = folders.Select(f => Task.Run(async () =>
        {
            var checkpointFolder = new CheckpointFolder(settingsManager, downloadService, modelFinder)
            {
                Title = Path.GetFileName(f), 
                DirectoryPath = f,
                IsExpanded = true, // Top level folders expanded by default
            };
            await checkpointFolder.IndexAsync();
            return checkpointFolder;
        })).ToList();
        
        await Task.WhenAll(indexTasks);
        
        // Set new observable collection, ordered by alphabetical order
        CheckpointFolders =
            new ObservableCollection<CheckpointFolder>(indexTasks
                .Select(t => t.Result)
                .OrderBy(f => f.Title));
    }
    
    [RelayCommand]
    [SupportedOSPlatform("windows")]
    private void OpenModelsFolder()
    {
        Process.Start("explorer.exe", settingsManager.ModelsDirectory);
    }
}
