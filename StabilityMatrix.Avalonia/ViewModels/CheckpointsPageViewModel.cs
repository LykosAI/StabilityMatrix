using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

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
    
    public ObservableCollection<CheckpointFolder> CheckpointFolders { get; set; } = new();
    
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
    
    [RelayCommand]
    [SupportedOSPlatform("windows")]
    private void OpenModelsFolder()
    {
        Process.Start("explorer.exe", settingsManager.ModelsDirectory);
    }
}
