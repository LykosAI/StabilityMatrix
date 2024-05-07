using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;
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

    public string NumImagesSelected =>
        NumItemsSelected == 1
            ? Resources.Label_OneImageSelected.Replace("images ", "")
            : string.Format(Resources.Label_NumImagesSelected, NumItemsSelected).Replace("images ", "");

    protected override void OnInitialLoaded()
    {
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
                    SelectedCategory?.Path == settingsManager.ModelsDirectory
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

        var sortComparer = SortExpressionComparer<CheckpointFileViewModel>
            .Ascending(vm => vm.CheckpointFile.SharedFolderType)
            .ThenByDescending(vm => vm.CheckpointFile.ConnectedModelInfo != null)
            .ThenByAscending(
                vm =>
                    vm.CheckpointFile.ConnectedModelInfo?.ModelName
                    ?? vm.CheckpointFile.FileNameWithoutExtension
            );

        ModelsCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(filterPredicate)
            .Filter(searchPredicate)
            .Transform(x => new CheckpointFileViewModel(x))
            .Sort(sortComparer)
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
            Refresh();
        };
    }

    public void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshCategories();
        ModelsCache.EditDiff(
            modelIndexService.ModelIndex.Values.SelectMany(x => x),
            (a, b) => a.RelativePath == b.RelativePath
        );
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
                        Count = Directory
                            .EnumerateFileSystemEntries(d, "*", SearchOption.AllDirectories)
                            .Count(
                                x =>
                                    CheckpointFile.SupportedCheckpointExtensions.Contains(
                                        Path.GetExtension(x)
                                    )
                            ),
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
}
