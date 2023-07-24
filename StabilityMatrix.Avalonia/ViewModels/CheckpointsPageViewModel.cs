using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

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

    public override IconSource IconSource => new SymbolIconSource
        {Symbol = Symbol.Notebook, IsFilled = true};

    // Toggle button for auto hashing new drag-and-dropped files for connected upgrade
    [ObservableProperty] private bool isImportAsConnected;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isIndexing;
    [ObservableProperty] private string searchFilter;

    partial void OnIsImportAsConnectedChanged(bool value)
    {
        if (settingsManager.IsLibraryDirSet &&
            value != settingsManager.Settings.IsImportAsConnected)
        {
            settingsManager.Transaction(s => s.IsImportAsConnected = value);
        }
    }

    [ObservableProperty] private ObservableCollection<CheckpointFolder> checkpointFolders = new();

    [ObservableProperty]
    private ObservableCollection<CheckpointFolder> displayedCheckpointFolders = new();

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
        DisplayedCheckpointFolders = CheckpointFolders;
        if (Design.IsDesignMode) return;

        // Set UI states
        IsImportAsConnected = settingsManager.Settings.IsImportAsConnected;
        SearchFilter = string.Empty;

        IsLoading = CheckpointFolders.Count == 0;
        IsIndexing = CheckpointFolders.Count > 0;
        await IndexFolders();
        IsLoading = false;
        IsIndexing = false;
    }

    partial void OnSearchFilterChanged(string value)
    {
        var filteredFolders = CheckpointFolders
            .Where(ContainsSearchFilter).ToList();
        foreach (var folder in filteredFolders)
        {
            folder.SearchFilter = SearchFilter;
        }

        DisplayedCheckpointFolders = new ObservableCollection<CheckpointFolder>(filteredFolders);
    }

    private bool ContainsSearchFilter(CheckpointFolder folder)
    {
        if (folder == null)
            throw new ArgumentNullException(nameof(folder));

        // Check files in the current folder
        return folder.CheckpointFiles.Any(x =>
                   x.FileName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)) ||
               // If no matching files were found in the current folder, check in all subfolders
               folder.SubFolders.Any(subFolder => ContainsSearchFilter(subFolder));
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
            var checkpointFolder =
                new CheckpointFolder(settingsManager, downloadService, modelFinder)
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
        DisplayedCheckpointFolders = new ObservableCollection<CheckpointFolder>(CheckpointFolders
            .Where(x => x.CheckpointFiles.Any(y => y.FileName.Contains(SearchFilter))));
    }

    [RelayCommand]
    private async Task OpenModelsFolder()
    {
        await ProcessRunner.OpenFolderBrowser(settingsManager.ModelsDirectory);
    }
}
