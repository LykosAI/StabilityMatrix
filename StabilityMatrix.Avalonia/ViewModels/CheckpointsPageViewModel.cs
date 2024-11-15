using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using CheckpointSortMode = StabilityMatrix.Core.Models.CheckpointSortMode;
using Notification = Avalonia.Controls.Notifications.Notification;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(CheckpointsPage))]
[Singleton]
public partial class CheckpointsPageViewModel(
    ILogger<CheckpointsPageViewModel> logger,
    ISettingsManager settingsManager,
    IModelIndexService modelIndexService,
    ModelFinder modelFinder,
    IDownloadService downloadService,
    INotificationService notificationService,
    IMetadataImportService metadataImportService,
    IModelImportService modelImportService,
    ServiceManager<ViewModelBase> dialogFactory
) : PageViewModelBase
{
    public override string Title => Resources.Label_CheckpointManager;

    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Notebook, IconVariant = IconVariant.Filled };

    private SourceCache<CheckpointCategory, string> categoriesCache = new(category => category.GetId());

    public IObservableCollection<CheckpointCategory> Categories { get; set; } =
        new ObservableCollectionExtended<CheckpointCategory>();

    public SourceCache<LocalModelFile, string> ModelsCache { get; } = new(file => file.RelativePath);

    public IObservableCollection<CheckpointFileViewModel> Models { get; set; } =
        new ObservableCollectionExtended<CheckpointFileViewModel>();

    [ObservableProperty]
    private bool showFolders = true;

    [ObservableProperty]
    private bool showModelsInSubfolders = true;

    [ObservableProperty]
    private CheckpointCategory? selectedCategory;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isImportAsConnectedEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NumImagesSelected))]
    private int numItemsSelected;

    [ObservableProperty]
    private bool sortConnectedModelsFirst = true;

    [ObservableProperty]
    private CheckpointSortMode selectedSortOption = CheckpointSortMode.Title;

    public List<CheckpointSortMode> SortOptions => Enum.GetValues<CheckpointSortMode>().ToList();

    [ObservableProperty]
    private ListSortDirection selectedSortDirection = ListSortDirection.Ascending;

    [ObservableProperty]
    private ProgressViewModel progress = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isDragOver;

    [ObservableProperty]
    private ObservableCollection<string> baseModelOptions =
        new(
            Enum.GetValues<CivitBaseModelType>()
                .Where(x => x != CivitBaseModelType.All)
                .Select(x => x.GetStringValue())
        );

    [ObservableProperty]
    private ObservableCollection<string> selectedBaseModels = [];

    [ObservableProperty]
    private bool dragMovesAllSelected = true;

    [ObservableProperty]
    private bool hideEmptyRootCategories;

    [ObservableProperty]
    private bool showNsfwImages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModelCardBottomResizeFactor))]
    private double resizeFactor;

    public double ModelCardBottomResizeFactor => Math.Clamp(ResizeFactor, 0.85d, 1.25d);

    public string ClearButtonText =>
        SelectedBaseModels.Count == BaseModelOptions.Count
            ? Resources.Action_ClearSelection
            : Resources.Action_SelectAll;

    public List<ListSortDirection> SortDirections => Enum.GetValues<ListSortDirection>().ToList();

    public string ModelsFolder => settingsManager.ModelsDirectory;

    public string NumImagesSelected =>
        NumItemsSelected == 1
            ? Resources.Label_OneImageSelected.Replace("images ", "")
            : string.Format(Resources.Label_NumImagesSelected, NumItemsSelected).Replace("images ", "");

    protected override void OnInitialLoaded()
    {
        if (Design.IsDesignMode)
            return;

        base.OnInitialLoaded();

        SelectedBaseModels = new ObservableCollection<string>(BaseModelOptions);
        SelectedBaseModels.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ClearButtonText));
            OnPropertyChanged(nameof(SelectedBaseModels));
            settingsManager.Transaction(
                settings => settings.SelectedBaseModels = SelectedBaseModels.ToList()
            );
        };

        // Observable predicate from SearchQuery changes
        var searchPredicate = this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(100))!
            .Select(
                _ =>
                    (Func<LocalModelFile, bool>)(
                        file =>
                            string.IsNullOrWhiteSpace(SearchQuery)
                            || file.DisplayModelFileName.Contains(
                                SearchQuery,
                                StringComparison.OrdinalIgnoreCase
                            )
                            || file.DisplayModelName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                            || file.DisplayModelVersion.Contains(
                                SearchQuery,
                                StringComparison.OrdinalIgnoreCase
                            )
                            || (
                                file.ConnectedModelInfo?.TrainedWordsString.Contains(
                                    SearchQuery,
                                    StringComparison.OrdinalIgnoreCase
                                ) ?? false
                            )
                    )
            )
            .AsObservable();

        var filterPredicate = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(
                x =>
                    x.EventArgs.PropertyName
                        is nameof(SelectedCategory)
                            or nameof(ShowModelsInSubfolders)
                            or nameof(SelectedBaseModels)
            )
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(_ => (Func<LocalModelFile, bool>)FilterModels)
            .AsObservable();

        var comparerObservable = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(
                x =>
                    x.EventArgs.PropertyName
                        is nameof(SelectedSortOption)
                            or nameof(SelectedSortDirection)
                            or nameof(SortConnectedModelsFirst)
            )
            .Select(_ =>
            {
                var comparer = new SortExpressionComparer<CheckpointFileViewModel>();
                if (SortConnectedModelsFirst)
                {
                    comparer = comparer.ThenByDescending(vm => vm.CheckpointFile.HasConnectedModel);
                }

                switch (SelectedSortOption)
                {
                    case CheckpointSortMode.FileName:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(vm => vm.CheckpointFile.FileName)
                                : comparer.ThenByDescending(vm => vm.CheckpointFile.FileName);
                        break;
                    case CheckpointSortMode.Title:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(vm => vm.CheckpointFile.DisplayModelName)
                                : comparer.ThenByDescending(vm => vm.CheckpointFile.DisplayModelName);
                        break;
                    case CheckpointSortMode.BaseModel:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(
                                    vm => vm.CheckpointFile.ConnectedModelInfo?.BaseModel
                                )
                                : comparer.ThenByDescending(
                                    vm => vm.CheckpointFile.ConnectedModelInfo?.BaseModel
                                );

                        comparer = comparer.ThenByAscending(vm => vm.CheckpointFile.DisplayModelName);
                        break;
                    case CheckpointSortMode.SharedFolderType:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(vm => vm.CheckpointFile.SharedFolderType)
                                : comparer.ThenByDescending(vm => vm.CheckpointFile.SharedFolderType);

                        comparer = comparer.ThenByAscending(vm => vm.CheckpointFile.DisplayModelName);
                        break;
                    case CheckpointSortMode.UpdateAvailable:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(vm => vm.CheckpointFile.HasUpdate)
                                : comparer.ThenByDescending(vm => vm.CheckpointFile.HasUpdate);
                        comparer = comparer.ThenByAscending(vm => vm.CheckpointFile.DisplayModelName);
                        comparer = comparer.ThenByDescending(vm => vm.CheckpointFile.DisplayModelVersion);
                        break;
                    case CheckpointSortMode.FileSize:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(vm => vm.FileSize)
                                : comparer.ThenByDescending(vm => vm.FileSize);
                        break;
                    case CheckpointSortMode.Created:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(vm => vm.Created)
                                : comparer.ThenByDescending(vm => vm.Created);
                        break;
                    case CheckpointSortMode.LastModified:
                        comparer =
                            SelectedSortDirection == ListSortDirection.Ascending
                                ? comparer.ThenByAscending(vm => vm.LastModified)
                                : comparer.ThenByDescending(vm => vm.LastModified);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return comparer;
            })
            .AsObservable();

        ModelsCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(filterPredicate)
            .Filter(searchPredicate)
            .Transform(
                x =>
                    new CheckpointFileViewModel(
                        settingsManager,
                        modelIndexService,
                        notificationService,
                        downloadService,
                        dialogFactory,
                        logger,
                        x
                    )
            )
            .SortAndBind(Models, comparerObservable)
            .WhenPropertyChanged(p => p.IsSelected)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ =>
            {
                NumItemsSelected = Models.Count(o => o.IsSelected);
            });

        var categoryFilterPredicate = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName is nameof(HideEmptyRootCategories))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(_ => (Func<CheckpointCategory, bool>)FilterCategories)
            .StartWith(FilterCategories)
            .AsObservable();

        categoriesCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(categoryFilterPredicate)
            .SortAndBind(
                Categories,
                SortExpressionComparer<CheckpointCategory>
                    .Descending(x => x.Name == "All Models")
                    .ThenByAscending(x => x.Name)
            )
            .Subscribe();

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsImportAsConnectedEnabled,
            s => s.IsImportAsConnected,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.ResizeFactor,
            s => s.CheckpointsPageResizeFactor,
            true
        );

        Refresh().SafeFireAndForget();

        EventManager.Instance.ModelIndexChanged += (_, _) =>
        {
            RefreshCategories();
            ModelsCache.EditDiff(
                modelIndexService.ModelIndex.Values.SelectMany(x => x),
                LocalModelFile.RelativePathConnectedModelInfoComparer
            );
        };

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.SortConnectedModelsFirst,
            settings => settings.SortConnectedModelsFirst,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.SelectedSortOption,
            settings => settings.CheckpointSortMode,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.SelectedSortDirection,
            settings => settings.CheckpointSortDirection,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.ShowModelsInSubfolders,
            settings => settings.ShowModelsInSubfolders,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.DragMovesAllSelected,
            settings => settings.DragMovesAllSelected,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.HideEmptyRootCategories,
            settings => settings.HideEmptyRootCategories,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.ShowNsfwImages,
            settings => settings.ShowNsfwInCheckpointsPage,
            true
        );

        // make sure a sort happens
        OnPropertyChanged(nameof(SortConnectedModelsFirst));
    }

    public void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await modelIndexService.RefreshIndex();
        Task.Run(async () => await modelIndexService.CheckModelsForUpdateAsync()).SafeFireAndForget();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        var selected = Models.Where(x => x.IsSelected).ToList();
        foreach (var model in selected)
        {
            model.IsSelected = false;
        }

        NumItemsSelected = 0;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (
            NumItemsSelected <= 0
            || Models.Where(o => o.IsSelected).Select(vm => vm.CheckpointFile).ToList()
                is not { Count: > 0 } selectedModelFiles
        )
        {
            return;
        }

        var pathsToDelete = selectedModelFiles
            .SelectMany(x => x.GetDeleteFullPaths(settingsManager.ModelsDirectory))
            .ToList();

        var confirmDeleteVm = dialogFactory.Get<ConfirmDeleteDialogViewModel>();
        confirmDeleteVm.PathsToDelete = pathsToDelete;

        if (await confirmDeleteVm.GetDialog().ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception e)
        {
            notificationService.ShowPersistent("Error deleting files", e.Message, NotificationType.Error);

            await modelIndexService.RefreshIndex();

            return;
        }

        await modelIndexService.RemoveModelsAsync(selectedModelFiles);

        NumItemsSelected = 0;
    }

    [RelayCommand]
    private async Task ScanMetadata(bool updateExistingMetadata)
    {
        if (SelectedCategory == null)
        {
            notificationService.Show(
                "No Category Selected",
                "Please select a category to scan for metadata.",
                NotificationType.Error
            );
            return;
        }

        var scanMetadataStep = new ScanMetadataStep(
            SelectedCategory.Path,
            metadataImportService,
            updateExistingMetadata
        );

        var runner = new PackageModificationRunner
        {
            ModificationCompleteMessage = "Metadata scan complete",
            HideCloseButton = false,
            ShowDialogOnStart = true
        };

        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await Dispatcher.UIThread.InvokeAsync(async () => await runner.ExecuteSteps([scanMetadataStep]));

        await modelIndexService.RefreshIndex();
        var message = updateExistingMetadata
            ? "Finished updating metadata."
            : "Finished scanning for missing metadata.";
        notificationService.Show("Scan Complete", message, NotificationType.Success);
    }

    [RelayCommand]
    private Task OnItemClick(CheckpointFileViewModel item)
    {
        // Select item if we're in "select mode"
        if (NumItemsSelected > 0)
        {
            item.IsSelected = !item.IsSelected;
        }
        else if (item.CheckpointFile.HasConnectedModel)
        {
            return ShowVersionDialog(item);
        }
        else
        {
            item.IsSelected = !item.IsSelected;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ShowVersionDialog(CheckpointFileViewModel item)
    {
        if (!item.CheckpointFile.HasCivitMetadata)
        {
            notificationService.Show(
                "Cannot show version dialog",
                "This model has custom metadata.",
                NotificationType.Error
            );
            return;
        }

        var model = item.CheckpointFile.LatestModelInfo;
        if (model is null)
        {
            notificationService.Show(
                "Model not found",
                "Model not found in index, please try again later.",
                NotificationType.Error
            );
            return;
        }

        var versions = model.ModelVersions;
        if (versions is null || versions.Count == 0)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            return;
        }

        item.IsLoading = true;

        var dialog = new BetterContentDialog
        {
            Title = model.Name,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            CloseOnClickOutside = true,
            MaxDialogWidth = 750,
            MaxDialogHeight = 950,
        };

        var prunedDescription = Utilities.RemoveHtml(model.Description);

        var viewModel = dialogFactory.Get<SelectModelVersionViewModel>();
        viewModel.Dialog = dialog;
        viewModel.Title = model.Name;
        viewModel.Description = prunedDescription;
        viewModel.CivitModel = model;
        viewModel.Versions = versions
            .Select(version => new ModelVersionViewModel(modelIndexService, version))
            .ToImmutableArray();
        viewModel.SelectedVersionViewModel = viewModel.Versions[0];

        dialog.Content = new SelectModelVersionDialog { DataContext = viewModel };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            DelayedClearViewModelProgress(item, TimeSpan.FromMilliseconds(100));
            return;
        }

        var selectedVersion = viewModel?.SelectedVersionViewModel?.ModelVersion;
        var selectedFile = viewModel?.SelectedFile?.CivitFile;

        DirectoryPath downloadPath;
        if (viewModel?.IsCustomSelected is true)
        {
            downloadPath = viewModel.CustomInstallLocation;
        }
        else
        {
            var subFolder =
                viewModel?.SelectedInstallLocation
                ?? Path.Combine(@"Models", model.Type.ConvertTo<SharedFolderType>().GetStringValue());
            subFolder = subFolder.StripStart(@$"Models{Path.DirectorySeparatorChar}");
            downloadPath = Path.Combine(settingsManager.ModelsDirectory, subFolder);
        }

        await Task.Delay(100);
        await modelImportService.DoImport(model, downloadPath, selectedVersion, selectedFile);

        item.Progress = new ProgressReport(1f, "Import started. Check the downloads tab for progress.");
        DelayedClearViewModelProgress(item, TimeSpan.FromMilliseconds(1000));
    }

    [RelayCommand]
    private void ClearOrSelectAllBaseModels()
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

    [RelayCommand]
    private async Task CreateFolder(object? treeViewItem)
    {
        if (treeViewItem is not CheckpointCategory category)
            return;

        var parentFolder = category.Path;

        if (string.IsNullOrWhiteSpace(parentFolder))
            return;

        var fields = new TextBoxField[]
        {
            new()
            {
                Label = "Folder Name",
                InnerLeftText =
                    $@"{parentFolder.Replace(settingsManager.ModelsDirectory, string.Empty).TrimStart(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}",
                MinWidth = 400
            }
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Create Folder", string.Empty, fields);
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        var folderName = fields[0].Text;
        var folderPath = Path.Combine(parentFolder, folderName);

        await notificationService.TryAsync(
            Task.Run(() => Directory.CreateDirectory(folderPath)),
            message: "Could not create folder"
        );

        RefreshCategories();

        SelectedCategory = Categories.SelectMany(c => c.Flatten()).FirstOrDefault(x => x.Path == folderPath);
    }

    [RelayCommand]
    private Task OpenFolderFromTreeview(object? treeViewItem) =>
        treeViewItem is CheckpointCategory category && !string.IsNullOrWhiteSpace(category.Path)
            ? ProcessRunner.OpenFolderBrowser(category.Path)
            : Task.CompletedTask;

    [RelayCommand]
    private async Task DeleteFolderAsync(object? treeViewItem)
    {
        if (treeViewItem is not CheckpointCategory category)
            return;

        var folderPath = category.Path;
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        var confirmDeleteVm = dialogFactory.Get<ConfirmDeleteDialogViewModel>();
        confirmDeleteVm.PathsToDelete = category.Flatten().Select(x => x.Path).ToList();

        if (await confirmDeleteVm.GetDialog().ShowAsync() != ContentDialogResult.Primary)
            return;

        confirmDeleteVm.PathsToDelete = [folderPath];

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception e)
        {
            notificationService.ShowPersistent("Error deleting folder", e.Message, NotificationType.Error);
            return;
        }

        RefreshCategories();
    }

    [RelayCommand]
    private void SelectAll()
    {
        Models.ForEach(x => x.IsSelected = true);
    }

    public async Task ImportFilesAsync(IEnumerable<string> files, DirectoryPath destinationFolder)
    {
        if (destinationFolder.FullPath == settingsManager.ModelsDirectory)
        {
            notificationService.Show(
                "Invalid Folder",
                "Please select a different folder to import the files into.",
                NotificationType.Error
            );
            return;
        }

        var fileList = files.ToList();
        if (
            fileList.Any(
                file => !LocalModelFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(file))
            )
        )
        {
            notificationService.Show(
                "Invalid File",
                "Please select only checkpoint files to import.",
                NotificationType.Error
            );
            return;
        }

        var importModelsStep = new ImportModelsStep(
            modelFinder,
            downloadService,
            modelIndexService,
            fileList,
            destinationFolder,
            IsImportAsConnectedEnabled,
            settingsManager.Settings.MoveFilesOnImport
        );

        var runner = new PackageModificationRunner
        {
            ModificationCompleteMessage = "Import Complete",
            HideCloseButton = false,
            ShowDialogOnStart = true
        };

        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps([importModelsStep]);

        SelectedCategory = Categories
            .SelectMany(c => c.Flatten())
            .FirstOrDefault(x => x.Path == destinationFolder.FullPath);
    }

    public async Task MoveBetweenFolders(LocalModelFile sourceFile, DirectoryPath destinationFolder)
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile.GetFullPath(settingsManager.ModelsDirectory));
        if (
            destinationFolder.FullPath == settingsManager.ModelsDirectory
            || (sourceDirectory != null && sourceDirectory == destinationFolder.FullPath)
        )
        {
            notificationService.Show(
                "Invalid Folder",
                "Please select a different folder to import the files into.",
                NotificationType.Error
            );
            return;
        }

        try
        {
            var sourcePath = new FilePath(sourceFile.GetFullPath(settingsManager.ModelsDirectory));
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
            var sourceCmInfoPath = Path.Combine(sourcePath.Directory!, $"{fileNameWithoutExt}.cm-info.json");
            var sourcePreviewPath = Path.Combine(sourcePath.Directory!, $"{fileNameWithoutExt}.preview.jpeg");
            var destinationFilePath = Path.Combine(destinationFolder, sourcePath.Name);
            var destinationCmInfoPath = Path.Combine(destinationFolder, $"{fileNameWithoutExt}.cm-info.json");
            var destinationPreviewPath = Path.Combine(
                destinationFolder,
                $"{fileNameWithoutExt}.preview.jpeg"
            );

            // Move files
            if (File.Exists(sourcePath))
            {
                await FileTransfers.MoveFileAsync(sourcePath, destinationFilePath);
            }

            if (File.Exists(sourceCmInfoPath))
            {
                await FileTransfers.MoveFileAsync(sourceCmInfoPath, destinationCmInfoPath);
            }

            if (File.Exists(sourcePreviewPath))
            {
                await FileTransfers.MoveFileAsync(sourcePreviewPath, destinationPreviewPath);
            }

            notificationService.Show(
                "Model moved successfully",
                $"Moved '{sourcePath.Name}' to '{Path.GetFileName(destinationFolder)}'"
            );
        }
        catch (FileTransferExistsException)
        {
            notificationService.Show(
                "Failed to move file",
                "Destination file exists",
                NotificationType.Error
            );
        }
        finally
        {
            SelectedCategory = Categories
                .SelectMany(c => c.Flatten())
                .FirstOrDefault(x => x.Path == destinationFolder.FullPath);

            await modelIndexService.RefreshIndex();
            DelayedClearProgress(TimeSpan.FromSeconds(1.5));
        }
    }

    private void RefreshCategories()
    {
        if (Design.IsDesignMode)
            return;

        if (!settingsManager.IsLibraryDirSet)
            return;

        var previouslySelectedCategory = SelectedCategory;

        var modelCategories = Directory
            .EnumerateDirectories(
                settingsManager.ModelsDirectory,
                "*",
                EnumerationOptionConstants.TopLevelOnly
            )
            .Select(
                d =>
                    new CheckpointCategory
                    {
                        Path = d,
                        Name = Path.GetFileName(d),
                        SubDirectories = GetSubfolders(d)
                    }
            )
            .ToList();

        foreach (var checkpointCategory in modelCategories.SelectMany(c => c.Flatten()))
        {
            checkpointCategory.Count = Directory
                .EnumerateFileSystemEntries(
                    checkpointCategory.Path,
                    "*",
                    EnumerationOptionConstants.AllDirectories
                )
                .Count(x => LocalModelFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(x)));
        }

        var rootCategory = new CheckpointCategory
        {
            Path = settingsManager.ModelsDirectory,
            Name = "All Models",
            Count = modelIndexService.ModelIndex.Values.SelectMany(x => x).Count(),
        };

        categoriesCache.Edit(updater =>
        {
            updater.Load([rootCategory, ..modelCategories]);
        });

        SelectedCategory =
            previouslySelectedCategory
            ?? Categories.FirstOrDefault(x => x.Path == previouslySelectedCategory?.Path)
            ?? Categories.FirstOrDefault()
            ?? categoriesCache.Items[0];

        var dirPath = new DirectoryPath(SelectedCategory.Path);

        while (dirPath.FullPath != settingsManager.ModelsDirectory && dirPath.Parent != null)
        {
            var category = Categories
                .SelectMany(x => x.Flatten())
                .FirstOrDefault(x => x.Path == dirPath.FullPath);
            if (category != null)
            {
                category.IsExpanded = true;
            }

            dirPath = dirPath.Parent;
        }

        Dispatcher.UIThread.Post(() =>
        {
            SelectedCategory =
                previouslySelectedCategory
                ?? Categories.FirstOrDefault(x => x.Path == previouslySelectedCategory?.Path)
                ?? Categories.FirstOrDefault()
                ?? categoriesCache.Items[0];
        });
    }

    private ObservableCollection<CheckpointCategory> GetSubfolders(string strPath)
    {
        var subfolders = new ObservableCollection<CheckpointCategory>();

        if (!Directory.Exists(strPath))
            return subfolders;

        var directories = Directory.EnumerateDirectories(
            strPath,
            "*",
            EnumerationOptionConstants.TopLevelOnly
        );

        foreach (var dir in directories)
        {
            var category = new CheckpointCategory
            {
                Name = Path.GetFileName(dir),
                Path = dir,
                Count = new DirectoryInfo(dir)
                    .EnumerateFileSystemInfos("*", EnumerationOptionConstants.AllDirectories)
                    .Count(x => LocalModelFile.SupportedCheckpointExtensions.Contains(x.Extension)),
            };

            if (Directory.GetDirectories(dir, "*", EnumerationOptionConstants.TopLevelOnly).Length > 0)
            {
                category.SubDirectories = GetSubfolders(dir);
            }

            subfolders.Add(category);
        }

        return subfolders;
    }

    private string GetConnectedModelInfoFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new InvalidOperationException(
                "Cannot get connected model info file path when filePath is empty"
            );
        }

        var modelNameNoExt = Path.GetFileNameWithoutExtension((string?)filePath);
        var modelDir = Path.GetDirectoryName((string?)filePath) ?? "";
        return Path.Combine(modelDir, $"{modelNameNoExt}.cm-info.json");
    }

    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay)
            .ContinueWith(_ =>
            {
                IsLoading = false;
                Progress.Value = 0;
            });
    }

    private void DelayedClearViewModelProgress(CheckpointFileViewModel viewModel, TimeSpan delay)
    {
        Task.Delay(delay)
            .ContinueWith(_ =>
            {
                viewModel.IsLoading = false;
                viewModel.Progress = new ProgressReport(0f, "");
            });
    }

    private bool FilterModels(LocalModelFile file)
    {
        if (SelectedCategory?.Path is null || SelectedCategory?.Path == settingsManager.ModelsDirectory)
        {
            return file.HasConnectedModel
                ? SelectedBaseModels.Contains(file.ConnectedModelInfo.BaseModel ?? "Other")
                : SelectedBaseModels.Contains("Other");
        }

        var folderPath = Path.GetDirectoryName(file.RelativePath);
        var categoryRelativePath = SelectedCategory
            ?.Path
            .Replace(settingsManager.ModelsDirectory, string.Empty)
            .TrimStart(Path.DirectorySeparatorChar);

        if (categoryRelativePath == null || folderPath == null)
        {
            return false;
        }

        if (
            (
                file.HasConnectedModel
                    ? SelectedBaseModels.Contains(file.ConnectedModelInfo?.BaseModel ?? "Other")
                    : SelectedBaseModels.Contains("Other")
            )
            is false
        )
        {
            return false;
        }

        return ShowModelsInSubfolders
            ? folderPath.StartsWith(categoryRelativePath)
            : categoryRelativePath.Equals(folderPath);
    }

    private bool FilterCategories(CheckpointCategory category)
    {
        return !HideEmptyRootCategories || category is { Count: > 0 };
    }
}
