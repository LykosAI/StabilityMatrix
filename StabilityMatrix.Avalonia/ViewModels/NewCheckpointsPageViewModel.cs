using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
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
using StabilityMatrix.Core.Services;
using CheckpointSortMode = StabilityMatrix.Core.Models.CheckpointSortMode;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(NewCheckpointsPage))]
[Singleton]
public partial class NewCheckpointsPageViewModel(
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
        new SymbolIconSource { Symbol = Symbol.Notebook, IsFilled = true };

    private SourceCache<CheckpointCategory, string> categoriesCache = new(category => category.Path);

    public IObservableCollection<CheckpointCategory> Categories { get; set; } =
        new ObservableCollectionExtended<CheckpointCategory>();

    public SourceCache<LocalModelFile, string> ModelsCache { get; } = new(file => file.RelativePath);
    public IObservableCollection<CheckpointFileViewModel> Models { get; set; } =
        new ObservableCollectionExtended<CheckpointFileViewModel>();

    [ObservableProperty]
    private bool showFolders = true;

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

        var filterPredicate = this.WhenPropertyChanged(vm => vm.SelectedCategory)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(
                _ =>
                    SelectedCategory?.Path is null
                    || SelectedCategory?.Path == settingsManager.ModelsDirectory
                        ? (Func<LocalModelFile, bool>)(_ => true)
                        : (Func<LocalModelFile, bool>)(
                            file =>
                                Path.GetDirectoryName(file.RelativePath)
                                    ?.Contains(
                                        SelectedCategory
                                            ?.Path
                                            .Replace(settingsManager.ModelsDirectory, string.Empty)
                                            .TrimStart(Path.DirectorySeparatorChar)
                                    )
                                    is true
                        )
            )
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
            .Transform(x => new CheckpointFileViewModel(settingsManager, modelIndexService, x))
            .Sort(comparerObservable)
            .Bind(Models)
            .WhenPropertyChanged(p => p.IsSelected)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ =>
            {
                NumItemsSelected = Models.Count(o => o.IsSelected);
            });

        categoriesCache.Connect().DeferUntilLoaded().Bind(Categories).Subscribe();
        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsImportAsConnectedEnabled,
            s => s.IsImportAsConnected,
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

        EventManager.Instance.DeleteModelRequested += OnDeleteModelRequested;

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

        // make sure a sort happens
        OnPropertyChanged(nameof(SortConnectedModelsFirst));
    }

    private void OnDeleteModelRequested(object? sender, string e)
    {
        if (sender is not CheckpointFileViewModel vm)
            return;

        DeleteModelAsync(vm).SafeFireAndForget();
        modelIndexService.RemoveModelAsync(vm.CheckpointFile).SafeFireAndForget();
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
        if (NumItemsSelected <= 0)
            return;

        var confirmationDialog = new BetterContentDialog
        {
            Title = string.Format(Resources.Label_AreYouSureDeleteModels, NumItemsSelected),
            Content = Resources.Label_ActionCannotBeUndone,
            PrimaryButtonText = Resources.Action_Delete,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = true,
        };

        var dialogResult = await confirmationDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
            return;

        var selectedModels = Models.Where(o => o.IsSelected).ToList();

        await Parallel.ForEachAsync(selectedModels, async (model, _) => await DeleteModelAsync(model));
        await modelIndexService.RemoveModelsAsync(selectedModels.Select(vm => vm.CheckpointFile));
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
            .Select(
                version =>
                    new ModelVersionViewModel(
                        settingsManager.Settings.InstalledModelHashes ?? new HashSet<string>(),
                        version
                    )
            )
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
            downloadPath = Path.Combine(settingsManager.LibraryDir, subFolder);
        }

        await Task.Delay(100);
        await modelImportService.DoImport(model, downloadPath, selectedVersion, selectedFile);

        item.Progress = new ProgressReport(1f, "Import started. Check the downloads tab for progress.");
        DelayedClearViewModelProgress(item, TimeSpan.FromMilliseconds(1000));
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

        var importModelsStep = new ImportModelsStep(
            modelFinder,
            downloadService,
            modelIndexService,
            files,
            destinationFolder,
            IsImportAsConnectedEnabled
        );

        var runner = new PackageModificationRunner
        {
            ModificationCompleteMessage = "Import Complete",
            HideCloseButton = false,
            ShowDialogOnStart = true
        };

        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps([importModelsStep]);
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
            var sourceCmInfoPath = Path.Combine(sourcePath.Directory, $"{fileNameWithoutExt}.cm-info.json");
            var sourcePreviewPath = Path.Combine(sourcePath.Directory, $"{fileNameWithoutExt}.preview.jpeg");
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

            await modelIndexService.RemoveModelAsync(sourceFile);

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

    private async Task DeleteModelAsync(CheckpointFileViewModel viewModel)
    {
        var filePath = viewModel.CheckpointFile.GetFullPath(settingsManager.ModelsDirectory);
        if (File.Exists(filePath))
        {
            viewModel.IsLoading = true;
            viewModel.Progress = new ProgressReport(0f, "Deleting...");
            try
            {
                await using var delay = new MinimumDelay(200, 500);
                await Task.Run(() => File.Delete(filePath));
                if (File.Exists(viewModel.ThumbnailUri))
                {
                    await Task.Run(() => File.Delete(viewModel.ThumbnailUri));
                }

                if (viewModel.CheckpointFile.HasConnectedModel)
                {
                    var cmInfoPath = GetConnectedModelInfoFilePath(filePath);
                    if (File.Exists(cmInfoPath))
                    {
                        await Task.Run(() => File.Delete(cmInfoPath));
                    }

                    settingsManager.Transaction(s =>
                    {
                        s.InstalledModelHashes?.Remove(
                            viewModel.CheckpointFile.ConnectedModelInfo.Hashes.BLAKE3
                        );
                    });
                }
            }
            catch (IOException ex)
            {
                // Logger.Warn($"Failed to delete checkpoint file {FilePath}: {ex.Message}");
                return; // Don't delete from collection
            }
            finally
            {
                viewModel.IsLoading = false;
            }
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
            .EnumerateDirectories(settingsManager.ModelsDirectory, "*", SearchOption.TopDirectoryOnly)
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

        var rootCategory = new CheckpointCategory
        {
            Path = settingsManager.ModelsDirectory,
            Name = "All Models",
            Count = modelIndexService.ModelIndex.Values.SelectMany(x => x).Count(),
        };

        categoriesCache.EditDiff(
            [rootCategory, ..modelCategories],
            (a, b) => a.Path == b.Path && a.SubDirectories.Count == b.SubDirectories.Count
        );

        SelectedCategory =
            previouslySelectedCategory
            ?? Categories.FirstOrDefault(x => x.Path == previouslySelectedCategory?.Path)
            ?? Categories.First();

        var sw = Stopwatch.StartNew();
        foreach (var checkpointCategory in Categories.SelectMany(c => c.Flatten()))
        {
            checkpointCategory.Count = Directory
                .EnumerateFileSystemEntries(checkpointCategory.Path, "*", SearchOption.AllDirectories)
                .Count(x => CheckpointFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(x)));
        }
        sw.Stop();
        Console.WriteLine($"counting took {sw.Elapsed.Milliseconds}ms");
    }

    private ObservableCollection<CheckpointCategory> GetSubfolders(string strPath)
    {
        var subfolders = new ObservableCollection<CheckpointCategory>();

        if (!Directory.Exists(strPath))
            return subfolders;

        var directories = Directory.EnumerateDirectories(strPath, "*", SearchOption.TopDirectoryOnly);

        foreach (var dir in directories)
        {
            var category = new CheckpointCategory
            {
                Name = Path.GetFileName(dir),
                Path = dir,
                Count = new DirectoryInfo(dir)
                    .EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                    .Count(x => CheckpointFile.SupportedCheckpointExtensions.Contains(x.Extension)),
            };

            if (Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly).Length > 0)
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
}
