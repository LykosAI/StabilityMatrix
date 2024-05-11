using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
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
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;
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
    INotificationService notificationService
) : PageViewModelBase
{
    public override string Title => Resources.Label_CheckpointManager;
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Cellular5g, IsFilled = true };

    private SourceCache<PackageOutputCategory, string> categoriesCache = new(category => category.Path);

    public IObservableCollection<PackageOutputCategory> Categories { get; set; } =
        new ObservableCollectionExtended<PackageOutputCategory>();

    public SourceCache<LocalModelFile, string> ModelsCache { get; } = new(file => file.RelativePath);
    public IObservableCollection<CheckpointFileViewModel> Models { get; set; } =
        new ObservableCollectionExtended<CheckpointFileViewModel>();

    [ObservableProperty]
    private bool showFolders = true;

    [ObservableProperty]
    private PackageOutputCategory? selectedCategory;

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
                            || file.FileNameWithoutExtension.Contains(
                                SearchQuery,
                                StringComparison.OrdinalIgnoreCase
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
            .Transform(x => new CheckpointFileViewModel(settingsManager, x))
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

        Refresh();

        EventManager.Instance.ModelIndexChanged += (_, _) =>
        {
            RefreshCategories();
            ModelsCache.EditDiff(
                modelIndexService.ModelIndex.Values.SelectMany(x => x),
                (a, b) => a.RelativePath == b.RelativePath
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
    private void Refresh()
    {
        modelIndexService.RefreshIndex();
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
                    new PackageOutputCategory
                    {
                        Path = d,
                        Name = Path.GetFileName(d),
                        SubDirectories = GetSubfolders(d)
                    }
            )
            .ToList();

        var rootCategory = new PackageOutputCategory
        {
            Path = settingsManager.ModelsDirectory,
            Name = "All Models",
            Count = modelIndexService.ModelIndex.Values.SelectMany(x => x).Count(),
        };

        categoriesCache.EditDiff([rootCategory, ..modelCategories], (a, b) => a.Path == b.Path);

        SelectedCategory = previouslySelectedCategory ?? Categories.First();

        foreach (var packageOutputCategory in Categories)
        {
            packageOutputCategory.Count = Directory
                .EnumerateFileSystemEntries(packageOutputCategory.Path, "*", SearchOption.AllDirectories)
                .Count(x => CheckpointFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(x)));
        }
    }

    private ObservableCollection<PackageOutputCategory> GetSubfolders(string strPath)
    {
        var subfolders = new ObservableCollection<PackageOutputCategory>();

        if (!Directory.Exists(strPath))
            return subfolders;

        var directories = Directory.EnumerateDirectories(strPath, "*", SearchOption.TopDirectoryOnly);

        foreach (var dir in directories)
        {
            var category = new PackageOutputCategory
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
}
