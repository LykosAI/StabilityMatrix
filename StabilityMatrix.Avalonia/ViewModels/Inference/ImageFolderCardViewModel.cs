using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageFolderCard))]
public partial class ImageFolderCardViewModel : ViewModelBase
{
    private readonly ILogger<ImageFolderCardViewModel> logger;
    private readonly IImageIndexService imageIndexService;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;

    /// <summary>
    /// Source of image files to display
    /// </summary>
    private readonly SourceCache<LocalImageFile, string> localImagesSource =
        new(imageFile => imageFile.RelativePath);

    /// <summary>
    /// Collection of image items to display
    /// </summary>
    public IObservableCollection<ImageFolderCardItemViewModel> Items { get; } =
        new ObservableCollectionExtended<ImageFolderCardItemViewModel>();

    /// <summary>
    /// Collection of local image files
    /// </summary>
    public IObservableCollection<LocalImageFile> LocalImages { get; } =
        new ObservableCollectionExtended<LocalImageFile>();

    public ImageFolderCardViewModel(
        ILogger<ImageFolderCardViewModel> logger,
        IImageIndexService imageIndexService,
        ISettingsManager settingsManager,
        INotificationService notificationService
    )
    {
        this.logger = logger;
        this.imageIndexService = imageIndexService;
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;

        // var minDatetime = DateTimeOffset.FromUnixTimeMilliseconds(0);

        /*localImagesSource
            .Connect()
            .DeferUntilLoaded()
            .Transform(
                imageFile =>
                    new ImageFolderCardItemViewModel
                    {
                        LocalImageFile = imageFile,
                        ImagePath = Design.IsDesignMode
                            ? imageFile.RelativePath
                            : imageFile.GetFullPath(settingsManager.ImagesDirectory)
                    }
            )
            .SortBy(x => x.LocalImageFile?.LastModifiedAt ?? minDatetime, SortDirection.Descending)
            .Bind(Items)
            .Subscribe();*/

        localImagesSource
            .Connect()
            .DeferUntilLoaded()
            .SortBy(file => file.LastModifiedAt, SortDirection.Descending)
            .Bind(LocalImages)
            .Subscribe();
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        await imageIndexService.RefreshIndex("Inference");

        var imageFiles = await imageIndexService.GetLocalImagesByPrefix("Inference");

        localImagesSource.Edit(x =>
        {
            x.Load(imageFiles);
        });
    }

    /// <summary>
    /// Handles image clicks to show preview
    /// </summary>
    [RelayCommand]
    private async Task OnImageClick(LocalImageFile item)
    {
        if (item.GetFullPath(settingsManager.ImagesDirectory) is not { } imagePath)
        {
            return;
        }

        var image = new ImageSource(new FilePath(imagePath));
        // Preload
        await image.GetBitmapAsync();

        var dialog = new BetterContentDialog
        {
            MaxDialogWidth = 1000,
            MaxDialogHeight = 1000,
            FullSizeDesired = true,
            IsFooterVisible = false,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new ImageViewerDialog
            {
                Width = 1000,
                Height = 1000,
                DataContext = new ImageViewerViewModel { ImageSource = image }
            }
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// Handles clicks to the image delete button
    /// </summary>
    [RelayCommand]
    private async Task OnImageDelete(LocalImageFile item)
    {
        if (item.GetFullPath(settingsManager.ImagesDirectory) is not { } imagePath)
        {
            return;
        }

        // Delete the file
        var result = await notificationService.TryAsync(new FilePath(imagePath).DeleteAsync());

        if (!result.IsSuccessful)
        {
            return;
        }

        // Remove from source
        localImagesSource.Remove(item);

        // Remove from index
        await imageIndexService.RemoveImage(item);
    }

    /// <summary>
    /// Handles clicks to the image open-in-explorer button
    /// </summary>
    [RelayCommand]
    private async Task OnImageOpen(LocalImageFile item)
    {
        if (item.GetFullPath(settingsManager.ImagesDirectory) is not { } imagePath)
        {
            return;
        }

        await ProcessRunner.OpenFileBrowser(imagePath);
    }
}
