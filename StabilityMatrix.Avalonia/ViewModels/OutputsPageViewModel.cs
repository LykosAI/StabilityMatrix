using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(OutputsPage))]
public partial class OutputsPageViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly INavigationService navigationService;
    public override string Title => "Outputs";

    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Grid, IsFilled = true };

    public SourceCache<LocalImageFile, string> OutputsCache { get; } = new(p => p.AbsolutePath);

    public IObservableCollection<LocalImageFile> Outputs { get; } =
        new ObservableCollectionExtended<LocalImageFile>();

    public IEnumerable<SharedOutputType> OutputTypes { get; } = Enum.GetValues<SharedOutputType>();

    [ObservableProperty]
    private ObservableCollection<PackageOutputCategory> categories;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowOutputTypes))]
    private PackageOutputCategory selectedCategory;

    [ObservableProperty]
    private SharedOutputType selectedOutputType;

    public bool CanShowOutputTypes => SelectedCategory.Name.Equals("Shared Output Folder");

    public OutputsPageViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        INotificationService notificationService,
        INavigationService navigationService
    )
    {
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.navigationService = navigationService;

        OutputsCache
            .Connect()
            .DeferUntilLoaded()
            .SortBy(x => x.CreatedAt, SortDirection.Descending)
            .Bind(Outputs)
            .Subscribe();

        if (!settingsManager.IsLibraryDirSet || Design.IsDesignMode)
            return;

        var packageCategories = settingsManager.Settings.InstalledPackages
            .Where(x => !x.UseSharedOutputFolder)
            .Select(p =>
            {
                var basePackage = packageFactory[p.PackageName!];
                if (basePackage is null)
                    return null;

                return new PackageOutputCategory
                {
                    Path = Path.Combine(p.FullPath, basePackage.OutputFolderName),
                    Name = p.DisplayName
                };
            })
            .ToList();

        packageCategories.Insert(
            0,
            new PackageOutputCategory
            {
                Path = settingsManager.ImagesDirectory,
                Name = "Shared Output Folder"
            }
        );

        Categories = new ObservableCollection<PackageOutputCategory>(packageCategories);
        SelectedCategory = Categories.First();
        SelectedOutputType = SharedOutputType.All;
    }

    public override void OnLoaded()
    {
        if (Design.IsDesignMode)
            return;

        GetOutputs(SelectedCategory.Path);
    }

    partial void OnSelectedCategoryChanged(
        PackageOutputCategory? oldValue,
        PackageOutputCategory? newValue
    )
    {
        if (oldValue == newValue || newValue == null)
            return;

        var path =
            CanShowOutputTypes && SelectedOutputType != SharedOutputType.All
                ? Path.Combine(newValue.Path, SelectedOutputType.ToString())
                : SelectedCategory.Path;
        GetOutputs(path);
    }

    partial void OnSelectedOutputTypeChanged(SharedOutputType oldValue, SharedOutputType newValue)
    {
        if (oldValue == newValue)
            return;

        var path =
            newValue == SharedOutputType.All
                ? SelectedCategory.Path
                : Path.Combine(SelectedCategory.Path, newValue.ToString());
        GetOutputs(path);
    }

    public async Task OnImageClick(LocalImageFile item)
    {
        var currentIndex = Outputs.IndexOf(item);

        var image = new ImageSource(new FilePath(item.AbsolutePath));

        // Preload
        await image.GetBitmapAsync();

        var vm = new ImageViewerViewModel { ImageSource = image, LocalImageFile = item };

        using var onNext = Observable
            .FromEventPattern<DirectionalNavigationEventArgs>(
                vm,
                nameof(ImageViewerViewModel.NavigationRequested)
            )
            .Subscribe(ctx =>
            {
                Dispatcher.UIThread
                    .InvokeAsync(async () =>
                    {
                        var sender = (ImageViewerViewModel)ctx.Sender!;
                        var newIndex = currentIndex + (ctx.EventArgs.IsNext ? 1 : -1);

                        if (newIndex >= 0 && newIndex < Outputs.Count)
                        {
                            var newImage = Outputs[newIndex];
                            var newImageSource = new ImageSource(
                                new FilePath(newImage.AbsolutePath)
                            );

                            // Preload
                            await newImageSource.GetBitmapAsync();

                            sender.ImageSource = newImageSource;
                            sender.LocalImageFile = newImage;

                            currentIndex = newIndex;
                        }
                    })
                    .SafeFireAndForget();
            });

        await vm.GetDialog().ShowAsync();
    }

    public async Task CopyImage(string imagePath)
    {
        var clipboard = App.Clipboard;

        await clipboard.SetFileDataObjectAsync(imagePath);
    }

    public async Task OpenImage(string imagePath) => await ProcessRunner.OpenFileBrowser(imagePath);

    public async Task DeleteImage(LocalImageFile? item)
    {
        if (item?.GetFullPath(settingsManager.ImagesDirectory) is not { } imagePath)
        {
            return;
        }

        // Delete the file
        var imageFile = new FilePath(imagePath);
        var result = await notificationService.TryAsync(imageFile.DeleteAsync());

        if (!result.IsSuccessful)
        {
            return;
        }

        Outputs.Remove(item);

        // Invalidate cache
        if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
        {
            loader.RemoveAllNamesFromCache(imageFile.Name);
        }
    }

    public void SendToTextToImage(LocalImageFile image)
    {
        navigationService.NavigateTo<InferenceViewModel>();
        EventManager.Instance.OnInferenceTextToImageRequested(image);
    }

    public void SendToUpscale(LocalImageFile image)
    {
        navigationService.NavigateTo<InferenceViewModel>();
        EventManager.Instance.OnInferenceUpscaleRequested(image);
    }

    private void GetOutputs(string directory)
    {
        if (!settingsManager.IsLibraryDirSet)
            return;

        if (!Directory.Exists(directory) && SelectedOutputType != SharedOutputType.All)
        {
            Directory.CreateDirectory(directory);
            return;
        }

        var list = Directory
            .EnumerateFiles(directory, "*.png", SearchOption.AllDirectories)
            .Select(file => LocalImageFile.FromPath(file))
            .OrderByDescending(f => f.CreatedAt);

        OutputsCache.EditDiff(list, (x, y) => x.AbsolutePath == y.AbsolutePath);
    }
}
