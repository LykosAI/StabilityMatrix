using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
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
    private readonly INotificationService notificationService;

    public override string Title => "Checkpoints";

    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Notebook, IsFilled = true };

    // Toggle button for auto hashing new drag-and-dropped files for connected upgrade
    [ObservableProperty]
    private bool isImportAsConnected;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isIndexing;

    [ObservableProperty]
    private bool showConnectedModelImages;

    [ObservableProperty]
    private string searchFilter = string.Empty;

    partial void OnIsImportAsConnectedChanged(bool value)
    {
        if (
            settingsManager.IsLibraryDirSet && value != settingsManager.Settings.IsImportAsConnected
        )
        {
            settingsManager.Transaction(s => s.IsImportAsConnected = value);
        }
    }

    public SourceCache<CheckpointFolder, string> CheckpointFoldersCache { get; } =
        new(x => x.DirectoryPath);

    public IObservableCollection<CheckpointFolder> CheckpointFolders { get; } =
        new ObservableCollectionExtended<CheckpointFolder>();

    public IObservableCollection<CheckpointFolder> DisplayedCheckpointFolders { get; } =
        new ObservableCollectionExtended<CheckpointFolder>();

    public CheckpointsPageViewModel(
        ISharedFolders sharedFolders,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        INotificationService notificationService,
        ModelFinder modelFinder
    )
    {
        this.sharedFolders = sharedFolders;
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
        this.notificationService = notificationService;
        this.modelFinder = modelFinder;

        CheckpointFoldersCache
            .Connect()
            .DeferUntilLoaded()
            .SortBy(x => x.Title)
            .Bind(CheckpointFolders)
            .Filter(ContainsSearchFilter)
            .Bind(DisplayedCheckpointFolders)
            .Subscribe();
    }

    public override async Task OnLoadedAsync()
    {
        var sw = Stopwatch.StartNew();
        // DisplayedCheckpointFolders = CheckpointFolders;

        // Set UI states
        IsImportAsConnected = settingsManager.Settings.IsImportAsConnected;
        ShowConnectedModelImages = settingsManager.Settings.ShowConnectedModelImages;
        // Refresh search filter
        OnSearchFilterChanged(string.Empty);

        if (Design.IsDesignMode)
            return;

        IsLoading = CheckpointFolders.Count == 0;
        IsIndexing = CheckpointFolders.Count > 0;
        // GetStuff();
        IndexFolders();
        IsLoading = false;
        IsIndexing = false;

        Logger.Info($"OnLoadedAsync in {sw.ElapsedMilliseconds}ms");
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSearchFilterChanged(string value)
    {
        foreach (var folder in CheckpointFolders)
        {
            folder.SearchFilter = value;
        }

        CheckpointFoldersCache.Refresh();
    }

    partial void OnShowConnectedModelImagesChanged(bool value)
    {
        if (
            settingsManager.IsLibraryDirSet
            && value != settingsManager.Settings.ShowConnectedModelImages
        )
        {
            settingsManager.Transaction(s => s.ShowConnectedModelImages = value);
        }
    }

    private bool ContainsSearchFilter(CheckpointFolder folder)
    {
        if (folder == null)
            throw new ArgumentNullException(nameof(folder));

        if (string.IsNullOrWhiteSpace(SearchFilter))
        {
            return true;
        }

        // Check files in the current folder
        return folder.CheckpointFiles.Any(
                x => x.FileName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
            )
            ||
            // If no matching files were found in the current folder, check in all subfolders
            folder.SubFolders.Any(ContainsSearchFilter);
    }

    private void IndexFolders()
    {
        var modelsDirectory = settingsManager.ModelsDirectory;

        // Setup shared folders in case they're missing
        sharedFolders.SetupSharedModelFolders();

        var folders = Directory.GetDirectories(modelsDirectory);

        var sw = Stopwatch.StartNew();

        // Index all folders

        foreach (var folder in folders)
        {
            // Get from cache or create new
            if (CheckpointFoldersCache.Lookup(folder) is { HasValue: true } result)
            {
                result.Value.Index();
            }
            else
            {
                var checkpointFolder = new CheckpointFolder(
                    settingsManager,
                    downloadService,
                    modelFinder,
                    notificationService
                )
                {
                    Title = Path.GetFileName(folder),
                    DirectoryPath = folder,
                    IsExpanded = true // Top level folders expanded by default
                };
                checkpointFolder.Index();
                CheckpointFoldersCache.AddOrUpdate(checkpointFolder);
            }
        }

        sw.Stop();
        Logger.Info($"IndexFolders in {sw.Elapsed.TotalMilliseconds:F1}ms");
    }

    /*private void GetStuff()
    {
        var allFiles = Directory.EnumerateFiles(
            settingsManager.ModelsDirectory,
            "*.*",
            SearchOption.AllDirectories
        );
        foreach (var file in allFiles)
        {
            var extension = Path.GetExtension(file);
            if (!CheckpointFile.SupportedCheckpointExtensions.Contains(extension))
                continue;

            var folder =
                Path.GetDirectoryName(file)
                    ?.Replace(
                        $"{settingsManager.ModelsDirectory}{Path.DirectorySeparatorChar}",
                        string.Empty
                    ) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(folder))
                continue;

            var isRootFolder = !folder.Contains(Path.DirectorySeparatorChar);
            var rootFolderName = isRootFolder
                ? folder
                : folder.Split(Path.DirectorySeparatorChar).First();

            // Get from cache or create new
            CheckpointFolder? rootCheckpointFolder;
            if (checkpointFoldersCache.Lookup(folder) is {HasValue: true} result)
            {
                rootCheckpointFolder = result.Value;
            }
            else
            {
                rootCheckpointFolder = new CheckpointFolder(
                    settingsManager,
                    downloadService,
                    modelFinder,
                    notificationService
                )
                {
                    Title = rootFolderName,
                    DirectoryPath = Path.Combine(settingsManager.ModelsDirectory, rootFolderName),
                    IsExpanded = isRootFolder, // Top level folders expanded by default
                };
                checkpointFoldersCache.AddOrUpdate(rootCheckpointFolder);
            }

            if (isRootFolder)
            {
                rootCheckpointFolder.CheckpointFiles.Add(
                    new CheckpointFile { Title = Path.GetFileName(file), FilePath = file }
                );
                continue;
            }

            // recursively add subfolders
            var subFolderNames = folder.Split(Path.DirectorySeparatorChar).Skip(1);

            foreach (var subFolderName in subFolderNames)
            {
                var subFolder = rootCheckpointFolder.SubFolders.FirstOrDefault(
                    x => x.Title == subFolderName
                );
                if (subFolder == null)
                {
                    subFolder = new CheckpointFolder(
                        settingsManager,
                        downloadService,
                        modelFinder,
                        notificationService
                    )
                    {
                        Title = subFolderName,
                        DirectoryPath = Path.Combine(
                            rootCheckpointFolder.DirectoryPath,
                            subFolderName
                        ),
                        ParentFolder = rootCheckpointFolder
                    };
                    rootCheckpointFolder.SubFolders.Add(subFolder);
                }
                rootCheckpointFolder = subFolder;
            }

            rootCheckpointFolder.CheckpointFiles.Add(
                new CheckpointFile { Title = Path.GetFileName(file), FilePath = file }
            );
        }
    }*/

    [RelayCommand]
    private async Task OpenModelsFolder()
    {
        await ProcessRunner.OpenFolderBrowser(settingsManager.ModelsDirectory);
    }
}
