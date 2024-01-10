using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;
using TeachingTip = StabilityMatrix.Core.Models.Settings.TeachingTip;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(CheckpointsPage))]
[Singleton]
public partial class CheckpointsPageViewModel : PageViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISharedFolders sharedFolders;
    private readonly ISettingsManager settingsManager;
    private readonly ModelFinder modelFinder;
    private readonly IDownloadService downloadService;
    private readonly INotificationService notificationService;
    private readonly IMetadataImportService metadataImportService;

    public override string Title => "Checkpoints";

    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Notebook, IsFilled = true };

    [ObservableProperty]
    private ObservableCollection<string> baseModelOptions =
        new(
            Enum.GetValues<CivitBaseModelType>()
                .Where(x => x != CivitBaseModelType.All)
                .Select(x => x.GetStringValue())
        );

    [ObservableProperty]
    private ObservableCollection<string> selectedBaseModels = [];

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

    [ObservableProperty]
    private bool isCategoryTipOpen;

    [ObservableProperty]
    private ProgressReport progress;

    partial void OnIsImportAsConnectedChanged(bool value)
    {
        if (settingsManager.IsLibraryDirSet && value != settingsManager.Settings.IsImportAsConnected)
        {
            settingsManager.Transaction(s => s.IsImportAsConnected = value);
        }
    }

    public SourceCache<CheckpointFolder, string> CheckpointFoldersCache { get; } = new(x => x.DirectoryPath);

    public IObservableCollection<CheckpointFolder> CheckpointFolders { get; } =
        new ObservableCollectionExtended<CheckpointFolder>();

    public IObservableCollection<CheckpointFolder> DisplayedCheckpointFolders { get; } =
        new ObservableCollectionExtended<CheckpointFolder>();

    public string ClearButtonText =>
        SelectedBaseModels.Count == BaseModelOptions.Count
            ? Resources.Action_ClearSelection
            : Resources.Action_SelectAll;

    private bool isClearing = false;

    public CheckpointsPageViewModel(
        ISharedFolders sharedFolders,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        INotificationService notificationService,
        IMetadataImportService metadataImportService,
        ModelFinder modelFinder
    )
    {
        this.sharedFolders = sharedFolders;
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
        this.notificationService = notificationService;
        this.metadataImportService = metadataImportService;
        this.modelFinder = modelFinder;

        SelectedBaseModels = new ObservableCollection<string>(BaseModelOptions);
        SelectedBaseModels.CollectionChanged += (_, _) =>
        {
            foreach (var folder in CheckpointFolders)
            {
                folder.BaseModelOptionsCache.EditDiff(SelectedBaseModels);
            }

            CheckpointFoldersCache.Refresh();
            OnPropertyChanged(nameof(ClearButtonText));
            if (!isClearing)
            {
                settingsManager.Transaction(
                    settings => settings.SelectedBaseModels = SelectedBaseModels.ToList()
                );
            }
        };

        CheckpointFoldersCache
            .Connect()
            .DeferUntilLoaded()
            .Bind(CheckpointFolders)
            .Filter(ContainsSearchFilter)
            .Filter(ContainsBaseModel)
            .SortBy(x => x.Title)
            .Bind(DisplayedCheckpointFolders)
            .Subscribe();
    }

    public override void OnLoaded()
    {
        base.OnLoaded();
        var sw = Stopwatch.StartNew();

        // Set UI states
        IsImportAsConnected = settingsManager.Settings.IsImportAsConnected;
        ShowConnectedModelImages = settingsManager.Settings.ShowConnectedModelImages;
        // Refresh search filter
        OnSearchFilterChanged(string.Empty);

        if (Design.IsDesignMode)
            return;

        if (!settingsManager.Settings.SeenTeachingTips.Contains(TeachingTip.CheckpointCategoriesTip))
        {
            IsCategoryTipOpen = true;
            settingsManager.Transaction(s => s.SeenTeachingTips.Add(TeachingTip.CheckpointCategoriesTip));
        }

        IsLoading = CheckpointFolders.Count == 0;
        IsIndexing = CheckpointFolders.Count > 0;
        IndexFolders();
        IsLoading = false;
        IsIndexing = false;

        isClearing = true;
        SelectedBaseModels.Clear();
        isClearing = false;

        SelectedBaseModels.AddRange(settingsManager.Settings.SelectedBaseModels);

        Logger.Info($"OnLoadedAsync in {sw.ElapsedMilliseconds}ms");
    }

    public void ClearSearchQuery()
    {
        SearchFilter = string.Empty;
    }

    public void ClearOrSelectAllBaseModels()
    {
        if (SelectedBaseModels.Count == BaseModelOptions.Count)
        {
            SelectedBaseModels.Clear();
        }
        else
        {
            SelectedBaseModels.Clear();
            SelectedBaseModels.AddRange(BaseModelOptions);
        }
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
        if (settingsManager.IsLibraryDirSet && value != settingsManager.Settings.ShowConnectedModelImages)
        {
            settingsManager.Transaction(s => s.ShowConnectedModelImages = value);
        }
    }

    private bool ContainsSearchFilter(CheckpointFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (string.IsNullOrWhiteSpace(SearchFilter))
        {
            return true;
        }

        // Check files in the current folder
        return folder.CheckpointFiles.Any(
                x =>
                    x.FileName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                    || x.Title.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                    || x.ConnectedModel?.ModelName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                        == true
                    || x.ConnectedModel?.Tags.Any(
                        t => t.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                    ) == true
                    || x.ConnectedModel?.TrainedWordsString.Contains(
                        SearchFilter,
                        StringComparison.OrdinalIgnoreCase
                    ) == true
            )
            ||
            // If no matching files were found in the current folder, check in all subfolders
            folder.SubFolders.Any(ContainsSearchFilter);
    }

    private bool ContainsBaseModel(CheckpointFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (SelectedBaseModels.Count == 0 || SelectedBaseModels.Count == BaseModelOptions.Count)
            return true;

        if (!folder.DisplayedCheckpointFiles.Any())
            return true;

        return folder.CheckpointFiles.Any(
                x =>
                    x.IsConnectedModel
                        ? SelectedBaseModels.Contains(x.ConnectedModel?.BaseModel)
                        : SelectedBaseModels.Contains("Other")
            ) || folder.SubFolders.Any(ContainsBaseModel);
    }

    private void IndexFolders()
    {
        var modelsDirectory = settingsManager.ModelsDirectory;

        // Setup shared folders in case they're missing
        sharedFolders.SetupSharedModelFolders();

        var folders = Directory.GetDirectories(modelsDirectory);

        var sw = Stopwatch.StartNew();

        var updatedFolders = new List<CheckpointFolder>();

        // Index all folders
        foreach (var folder in folders)
        {
            // Get from cache or create new
            if (CheckpointFoldersCache.Lookup(folder) is { HasValue: true } result)
            {
                result.Value.Index();
                updatedFolders.Add(result.Value);
            }
            else
            {
                var checkpointFolder = new CheckpointFolder(
                    settingsManager,
                    downloadService,
                    modelFinder,
                    notificationService,
                    metadataImportService
                )
                {
                    Title = Path.GetFileName(folder),
                    DirectoryPath = folder,
                    IsExpanded = true // Top level folders expanded by default
                };
                checkpointFolder.Index();
                updatedFolders.Add(checkpointFolder);
            }
        }

        CheckpointFoldersCache.EditDiff(updatedFolders, (a, b) => a.Title == b.Title);

        sw.Stop();
        Logger.Info($"IndexFolders in {sw.Elapsed.TotalMilliseconds:F1}ms");
    }

    [RelayCommand]
    private async Task OpenModelsFolder()
    {
        await ProcessRunner.OpenFolderBrowser(settingsManager.ModelsDirectory);
    }

    [RelayCommand]
    private async Task FindConnectedMetadata()
    {
        var progressHandler = new Progress<ProgressReport>(report =>
        {
            Progress = report;
        });

        await metadataImportService.ScanDirectoryForMissingInfo(
            settingsManager.ModelsDirectory,
            progressHandler
        );

        notificationService.Show(
            "Scan Complete",
            "Finished scanning for missing metadata.",
            NotificationType.Success
        );

        DelayedClearProgress(TimeSpan.FromSeconds(1.5));
    }

    [RelayCommand]
    private async Task UpdateExistingMetadata()
    {
        var progressHandler = new Progress<ProgressReport>(report =>
        {
            Progress = report;
        });

        await metadataImportService.UpdateExistingMetadata(settingsManager.ModelsDirectory, progressHandler);
        notificationService.Show("Scan Complete", "Finished updating metadata.", NotificationType.Success);

        DelayedClearProgress(TimeSpan.FromSeconds(1.5));
    }

    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay)
            .ContinueWith(_ =>
            {
                IsLoading = false;
                Progress = new ProgressReport(0, 0);
            });
    }
}
