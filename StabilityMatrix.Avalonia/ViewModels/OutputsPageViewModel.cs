using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
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
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Size = StabilityMatrix.Core.Models.Settings.Size;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

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
    public override string Title => Resources.Label_OutputsPageTitle;

    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Grid, IsFilled = true };

    public SourceCache<LocalImageFile, string> OutputsCache { get; } = new(file => file.AbsolutePath);

    public IObservableCollection<OutputImageViewModel> Outputs { get; set; } =
        new ObservableCollectionExtended<OutputImageViewModel>();

    public IEnumerable<SharedOutputType> OutputTypes { get; } = Enum.GetValues<SharedOutputType>();

    [ObservableProperty]
    private ObservableCollection<PackageOutputCategory> categories;

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

    public bool CanShowOutputTypes => SelectedCategory?.Name?.Equals("Shared Output Folder") ?? false;

    public string NumImagesSelected =>
        NumItemsSelected == 1
            ? Resources.Label_OneImageSelected
            : string.Format(Resources.Label_NumImagesSelected, NumItemsSelected);

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
            .Throttle(TimeSpan.FromMilliseconds(50))!
            .Select(property => searcher.GetPredicate(property.Value))
            .AsObservable();

        OutputsCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(searchPredicate)
            .Transform(file => new OutputImageViewModel(file))
            .SortBy(vm => vm.ImageFile.CreatedAt, SortDirection.Descending)
            .Bind(Outputs)
            .WhenPropertyChanged(p => p.IsSelected)
            .Subscribe(_ =>
            {
                NumItemsSelected = Outputs.Count(o => o.IsSelected);
            });

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.ImageSize,
            settings => settings.OutputsImageSize,
            delay: TimeSpan.FromMilliseconds(250)
        );

        RefreshCategories(false);
    }

    public override void OnLoaded()
    {
        if (Design.IsDesignMode)
            return;

        if (!settingsManager.IsLibraryDirSet)
            return;

        Directory.CreateDirectory(settingsManager.ImagesDirectory);

        SelectedCategory ??= Categories.First();
        SelectedOutputType ??= SharedOutputType.All;
        SearchQuery = string.Empty;
        ImageSize = settingsManager.Settings.OutputsImageSize;

        var path =
            CanShowOutputTypes && SelectedOutputType != SharedOutputType.All
                ? Path.Combine(SelectedCategory.Path, SelectedOutputType.ToString())
                : SelectedCategory.Path;
        GetOutputs(path);
    }

    partial void OnSelectedCategoryChanged(PackageOutputCategory? oldValue, PackageOutputCategory? newValue)
    {
        if (oldValue == newValue || newValue == null)
            return;

        var path =
            CanShowOutputTypes && SelectedOutputType != SharedOutputType.All
                ? Path.Combine(newValue.Path, SelectedOutputType.ToString())
                : SelectedCategory.Path;
        GetOutputs(path);
    }

    partial void OnSelectedOutputTypeChanged(SharedOutputType? oldValue, SharedOutputType? newValue)
    {
        if (oldValue == newValue || newValue == null)
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
        Dispatcher.UIThread.Post(() => RefreshCategories());
        Dispatcher.UIThread.Post(OnLoaded);
    }

    public async Task DeleteImage(OutputImageViewModel? item)
    {
        if (item is null)
            return;

        var confirmationDialog = new BetterContentDialog
        {
            Title = "Are you sure you want to delete this image?",
            Content = "This action cannot be undone.",
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
            Title = $"Are you sure you want to delete {NumItemsSelected} images?",
            Content = "This action cannot be undone.",
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

            OutputsCache.Remove(output.ImageFile);

            // Invalidate cache
            if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
            {
                loader.RemoveAllNamesFromCache(imageFile.Name);
            }
        }

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

            foreach (var path in Directory.EnumerateFiles(directory, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    var file = new FilePath(path);
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

        OnLoaded();
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

        var files = Directory
            .EnumerateFiles(directory, "*.png", SearchOption.AllDirectories)
            .Select(file => LocalImageFile.FromPath(file))
            .ToList();

        if (files.Count == 0)
        {
            OutputsCache.Clear();
        }
        else
        {
            OutputsCache.EditDiff(files);
        }
    }

    private void RefreshCategories(bool updateProperty = true)
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
            .Where(p => p.BasePackage.SharedOutputFolders != null && p.BasePackage.SharedOutputFolders.Any())
            .Select(
                pair =>
                    new PackageOutputCategory
                    {
                        Path = Path.Combine(
                            pair.InstalledPackage.FullPath!,
                            pair.BasePackage.OutputFolderName
                        ),
                        Name = pair.InstalledPackage.DisplayName ?? ""
                    }
            )
            .ToList();

        packageCategories.Insert(
            0,
            new PackageOutputCategory
            {
                Path = settingsManager.ImagesDirectory,
                Name = "Shared Output Folder"
            }
        );

        packageCategories.Insert(
            1,
            new PackageOutputCategory { Path = settingsManager.ImagesInferenceDirectory, Name = "Inference" }
        );

        Categories = new ObservableCollection<PackageOutputCategory>(packageCategories);

        if (updateProperty)
        {
            SelectedCategory =
                Categories.FirstOrDefault(x => x.Name == previouslySelectedCategory?.Name)
                ?? Categories.First();
        }
        else
        {
            selectedCategory =
                Categories.FirstOrDefault(x => x.Name == previouslySelectedCategory?.Name)
                ?? Categories.First();
        }
    }
}
