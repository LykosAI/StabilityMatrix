using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Nito.Disposables.Internals;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.OutputsPage;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Size = StabilityMatrix.Core.Models.Settings.Size;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(Views.OutputsPage))]
[Singleton]
public partial class OutputsPageViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly INotificationService notificationService;
    private readonly INavigationService<MainWindowViewModel> navigationService;
    private readonly ILogger<OutputsPageViewModel> logger;
    private readonly List<CancellationTokenSource> cancellationTokenSources = [];

    public override string Title => Resources.Label_OutputsPageTitle;

    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Grid, IsFilled = true };

    public SourceCache<LocalImageFile, string> OutputsCache { get; } = new(file => file.AbsolutePath);

    private SourceCache<PackageOutputCategory, string> categoriesCache = new(category => category.Path);

    public IObservableCollection<OutputImageViewModel> Outputs { get; set; } =
        new ObservableCollectionExtended<OutputImageViewModel>();

    public IObservableCollection<PackageOutputCategory> Categories { get; set; } =
        new ObservableCollectionExtended<PackageOutputCategory>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowOutputTypes))]
    private PackageOutputCategory? selectedCategory;

    [ObservableProperty]
    private SharedOutputType? selectedOutputType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NumImagesSelected))]
    private int numItemsSelected;

    [ObservableProperty]
    private string searchQuery;

    [ObservableProperty]
    private Size imageSize = new(300, 300);

    [ObservableProperty]
    private bool isConsolidating;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool showFolders;

    [ObservableProperty]
    private bool isChangingCategory;

    public bool CanShowOutputTypes => SelectedCategory?.Name?.Equals("Shared Output Folder") ?? false;

    public string NumImagesSelected =>
        NumItemsSelected == 1
            ? Resources.Label_OneImageSelected
            : string.Format(Resources.Label_NumImagesSelected, NumItemsSelected);

    private string[] allowedExtensions = [".png", ".webp"];

    private PackageOutputCategory? lastOutputCategory;

    public OutputsPageViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        INotificationService notificationService,
        INavigationService<MainWindowViewModel> navigationService,
        ILogger<OutputsPageViewModel> logger
    )
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.notificationService = notificationService;
        this.navigationService = navigationService;
        this.logger = logger;

        var searcher = new ImageSearcher();

        // Observable predicate from SearchQuery changes
        var searchPredicate = this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(100))!
            .Select(property => searcher.GetPredicate(property.Value))
            .AsObservable();

        OutputsCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(searchPredicate)
            .Transform(file => new OutputImageViewModel(file))
            .Sort(
                SortExpressionComparer<OutputImageViewModel>
                    .Descending(vm => vm.ImageFile.CreatedAt)
                    .ThenByDescending(vm => vm.ImageFile.FileName)
            )
            .Bind(Outputs)
            .WhenPropertyChanged(p => p.IsSelected)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ =>
            {
                NumItemsSelected = Outputs.Count(o => o.IsSelected);
            });

        categoriesCache.Connect().DeferUntilLoaded().Bind(Categories).Subscribe();

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.ImageSize,
            settings => settings.OutputsImageSize,
            delay: TimeSpan.FromMilliseconds(250)
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.ShowFolders,
            settings => settings.IsOutputsTreeViewEnabled,
            true
        );
    }

    protected override void OnInitialLoaded()
    {
        if (Design.IsDesignMode)
            return;

        if (!settingsManager.IsLibraryDirSet)
            return;

        Directory.CreateDirectory(settingsManager.ImagesDirectory);

        RefreshCategories();

        SelectedCategory ??= Categories.First();
        SelectedOutputType ??= SharedOutputType.All;
        SearchQuery = string.Empty;
        ImageSize = settingsManager.Settings.OutputsImageSize;
        lastOutputCategory = SelectedCategory;

        IsChangingCategory = true;

        var path =
            CanShowOutputTypes && SelectedOutputType != SharedOutputType.All
                ? Path.Combine(SelectedCategory.Path, SelectedOutputType.ToString())
                : SelectedCategory.Path;
        GetOutputs(path);
    }

    partial void OnSelectedCategoryChanged(PackageOutputCategory? oldValue, PackageOutputCategory? newValue)
    {
        if (oldValue == newValue || oldValue == null || newValue == null)
            return;

        var path =
            CanShowOutputTypes && SelectedOutputType != SharedOutputType.All
                ? Path.Combine(newValue.Path, SelectedOutputType.ToString())
                : SelectedCategory.Path;
        GetOutputs(path);
        lastOutputCategory = newValue;
    }

    partial void OnSelectedOutputTypeChanged(SharedOutputType? oldValue, SharedOutputType? newValue)
    {
        if (oldValue == newValue || oldValue == null || newValue == null)
            return;

        var path =
            newValue == SharedOutputType.All
                ? SelectedCategory?.Path
                : Path.Combine(SelectedCategory.Path, newValue.ToString());
        GetOutputs(path);
    }

    public Task OnImageClick(OutputImageViewModel item)
    {
        // Select image if we're in "select mode"
        if (NumItemsSelected > 0)
        {
            item.IsSelected = !item.IsSelected;
        }
        else
        {
            return ShowImageDialog(item);
        }

        return Task.CompletedTask;
    }

    public async Task ShowImageDialog(OutputImageViewModel item)
    {
        var currentIndex = Outputs.IndexOf(item);

        var image = new ImageSource(new FilePath(item.ImageFile.AbsolutePath));

        // Preload
        await image.GetBitmapAsync();

        var vm = new ImageViewerViewModel { ImageSource = image, LocalImageFile = item.ImageFile };

        using var onNext = Observable
            .FromEventPattern<DirectionalNavigationEventArgs>(
                vm,
                nameof(ImageViewerViewModel.NavigationRequested)
            )
            .Subscribe(ctx =>
            {
                Dispatcher
                    .UIThread.InvokeAsync(async () =>
                    {
                        var sender = (ImageViewerViewModel)ctx.Sender!;
                        var newIndex = currentIndex + (ctx.EventArgs.IsNext ? 1 : -1);

                        if (newIndex >= 0 && newIndex < Outputs.Count)
                        {
                            var newImage = Outputs[newIndex];
                            var newImageSource = new ImageSource(
                                new FilePath(newImage.ImageFile.AbsolutePath)
                            );

                            // Preload
                            await newImageSource.GetBitmapAsync();

                            sender.ImageSource = newImageSource;
                            sender.LocalImageFile = newImage.ImageFile;

                            currentIndex = newIndex;
                        }
                    })
                    .SafeFireAndForget();
            });

        await vm.GetDialog().ShowAsync();
    }

    public Task CopyImage(string imagePath)
    {
        var clipboard = App.Clipboard;
        return clipboard.SetFileDataObjectAsync(imagePath);
    }

    public Task OpenImage(string imagePath) => ProcessRunner.OpenFileBrowser(imagePath);

    public void Refresh()
    {
        Dispatcher.UIThread.Post(RefreshCategories);

        var path =
            CanShowOutputTypes && SelectedOutputType != SharedOutputType.All
                ? Path.Combine(SelectedCategory.Path, SelectedOutputType.ToString())
                : SelectedCategory.Path;
        GetOutputs(path);
    }

    public async Task DeleteImage(OutputImageViewModel? item)
    {
        if (item is null)
            return;

        var confirmationDialog = new BetterContentDialog
        {
            Title = Resources.Label_AreYouSure,
            Content = Resources.Label_ActionCannotBeUndone,
            PrimaryButtonText = Resources.Action_Delete,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = true,
        };
        var dialogResult = await confirmationDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
            return;

        // Delete the file
        var imageFile = new FilePath(item.ImageFile.AbsolutePath);
        var result = await notificationService.TryAsync(imageFile.DeleteAsync());

        if (!result.IsSuccessful)
        {
            return;
        }
        //Attempt to remove .txt sidecar if it exists
        var sideCar = new FilePath(Path.ChangeExtension(imageFile, ".txt"));
        if (File.Exists(sideCar))
        {
            await notificationService.TryAsync(sideCar.DeleteAsync());
        }

        OutputsCache.Remove(item.ImageFile);

        // Invalidate cache
        if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
        {
            loader.RemoveAllNamesFromCache(imageFile.Name);
        }
    }

    public void SendToTextToImage(OutputImageViewModel vm)
    {
        navigationService.NavigateTo<InferenceViewModel>();
        EventManager.Instance.OnInferenceTextToImageRequested(vm.ImageFile);
    }

    public void SendToUpscale(OutputImageViewModel vm)
    {
        navigationService.NavigateTo<InferenceViewModel>();
        EventManager.Instance.OnInferenceUpscaleRequested(vm.ImageFile);
    }

    public void SendToImageToImage(OutputImageViewModel vm)
    {
        navigationService.NavigateTo<InferenceViewModel>();
        EventManager.Instance.OnInferenceImageToImageRequested(vm.ImageFile);
    }

    public void SendToImageToVideo(OutputImageViewModel vm)
    {
        navigationService.NavigateTo<InferenceViewModel>();
        EventManager.Instance.OnInferenceImageToVideoRequested(vm.ImageFile);
    }

    public void ClearSelection()
    {
        foreach (var output in Outputs)
        {
            output.IsSelected = false;
        }
    }

    public void SelectAll()
    {
        foreach (var output in Outputs)
        {
            output.IsSelected = true;
        }
    }

    public async Task DeleteAllSelected()
    {
        var confirmationDialog = new BetterContentDialog
        {
            Title = string.Format(Resources.Label_AreYouSureDeleteImages, NumItemsSelected),
            Content = Resources.Label_ActionCannotBeUndone,
            PrimaryButtonText = Resources.Action_Delete,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = true,
        };
        var dialogResult = await confirmationDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
            return;

        var selected = Outputs.Where(o => o.IsSelected).ToList();
        Debug.Assert(selected.Count == NumItemsSelected);

        var imagesToRemove = new List<LocalImageFile>();
        foreach (var output in selected)
        {
            // Delete the file
            var imageFile = new FilePath(output.ImageFile.AbsolutePath);
            var result = await notificationService.TryAsync(imageFile.DeleteAsync());
            if (!result.IsSuccessful)
            {
                continue;
            }

            //Attempt to remove .txt sidecar if it exists
            var sideCar = new FilePath(Path.ChangeExtension(imageFile, ".txt"));
            if (File.Exists(sideCar))
            {
                await notificationService.TryAsync(sideCar.DeleteAsync());
            }

            imagesToRemove.Add(output.ImageFile);
        }

        OutputsCache.Remove(imagesToRemove);
        NumItemsSelected = 0;
        ClearSelection();
    }

    public async Task ConsolidateImages()
    {
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(
            new TextBlock
            {
                Text = Resources.Label_ConsolidateExplanation,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 16)
            }
        );
        foreach (var category in Categories)
        {
            if (category.Name == "Shared Output Folder")
            {
                continue;
            }

            stackPanel.Children.Add(
                new CheckBox
                {
                    Content = $"{category.Name} ({category.Path})",
                    IsChecked = true,
                    Margin = new Thickness(0, 8, 0, 0),
                    Tag = category.Path
                }
            );
        }

        var confirmationDialog = new BetterContentDialog
        {
            Title = Resources.Label_AreYouSure,
            Content = stackPanel,
            PrimaryButtonText = Resources.Action_Yes,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = true,
        };

        var dialogResult = await confirmationDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
            return;

        IsConsolidating = true;

        Directory.CreateDirectory(settingsManager.ConsolidatedImagesDirectory);

        foreach (var category in stackPanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true))
        {
            if (
                string.IsNullOrWhiteSpace(category.Tag?.ToString())
                || !Directory.Exists(category.Tag?.ToString())
            )
                continue;

            var directory = category.Tag.ToString();

            foreach (var path in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    var file = new FilePath(path);
                    if (!allowedExtensions.Contains(file.Extension))
                        continue;

                    var newPath = settingsManager.ConsolidatedImagesDirectory + file.Name;
                    if (file.FullPath == newPath)
                        continue;

                    // ignore inference if not in inference directory
                    if (
                        file.FullPath.Contains(settingsManager.ImagesInferenceDirectory)
                        && directory != settingsManager.ImagesInferenceDirectory
                    )
                    {
                        continue;
                    }

                    await file.MoveToWithIncrementAsync(newPath);

                    var sideCar = new FilePath(Path.ChangeExtension(file, ".txt"));
                    //If a .txt sidecar file exists, and the image was moved successfully, try to move the sidecar along with the image
                    if (File.Exists(newPath) && File.Exists(sideCar))
                    {
                        var newSidecar = new FilePath(Path.ChangeExtension(newPath, ".txt"));
                        await sideCar.MoveToWithIncrementAsync(newSidecar);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error when consolidating: ");
                }
            }
        }

        Refresh();
        IsConsolidating = false;
    }

    public void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }

    private void GetOutputs(string directory)
    {
        if (!settingsManager.IsLibraryDirSet)
            return;

        if (
            !Directory.Exists(directory)
            && (
                SelectedCategory.Path != settingsManager.ImagesDirectory
                || SelectedOutputType != SharedOutputType.All
            )
        )
        {
            Directory.CreateDirectory(directory);
            return;
        }

        if (lastOutputCategory?.Path.Equals(directory) is not true)
        {
            OutputsCache.Clear();
            IsChangingCategory = true;
        }

        IsLoading = true;

        cancellationTokenSources.ForEach(cts => cts.Cancel());

        Task.Run(() =>
        {
            var getOutputsTokenSource = new CancellationTokenSource();
            cancellationTokenSources.Add(getOutputsTokenSource);

            if (getOutputsTokenSource.IsCancellationRequested)
            {
                cancellationTokenSources.Remove(getOutputsTokenSource);
                return;
            }

            var files = Directory
                .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(file => allowedExtensions.Contains(new FilePath(file).Extension))
                .Select(file => LocalImageFile.FromPath(file))
                .ToList();

            if (getOutputsTokenSource.IsCancellationRequested)
            {
                cancellationTokenSources.Remove(getOutputsTokenSource);
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (files.Count == 0 && OutputsCache.Count == 0)
                {
                    IsLoading = false;
                    IsChangingCategory = false;
                    return;
                }

                OutputsCache.EditDiff(
                    files,
                    (oldItem, newItem) => oldItem.AbsolutePath == newItem.AbsolutePath
                );

                IsLoading = false;
                IsChangingCategory = false;
            });
            cancellationTokenSources.Remove(getOutputsTokenSource);
        });
    }

    private void RefreshCategories()
    {
        if (Design.IsDesignMode)
            return;

        if (!settingsManager.IsLibraryDirSet)
            return;

        var previouslySelectedCategory = SelectedCategory;

        var packageCategories = settingsManager
            .Settings.InstalledPackages.Where(x => !x.UseSharedOutputFolder)
            .Select(packageFactory.GetPackagePair)
            .WhereNotNull()
            .Where(
                p =>
                    p.BasePackage.SharedOutputFolders is { Count: > 0 } && p.InstalledPackage.FullPath != null
            )
            .Select(
                pair =>
                    new PackageOutputCategory
                    {
                        Path = Path.Combine(
                            pair.InstalledPackage.FullPath!,
                            pair.BasePackage.OutputFolderName
                        ),
                        Name = pair.InstalledPackage.DisplayName ?? "",
                        SubDirectories = GetSubfolders(
                            Path.Combine(pair.InstalledPackage.FullPath!, pair.BasePackage.OutputFolderName)
                        )
                    }
            )
            .ToList();

        packageCategories.Insert(
            0,
            new PackageOutputCategory
            {
                Path = settingsManager.ImagesDirectory,
                Name = "Shared Output Folder",
                SubDirectories = GetSubfolders(settingsManager.ImagesDirectory)
            }
        );

        categoriesCache.EditDiff(packageCategories, (a, b) => a.Path == b.Path);

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
            var category = new PackageOutputCategory { Name = Path.GetFileName(dir), Path = dir };

            if (Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly).Length > 0)
            {
                category.SubDirectories = GetSubfolders(dir);
            }

            subfolders.Add(category);
        }

        return subfolders;
    }
}
