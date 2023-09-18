using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Navigation;
using FuzzySharp;
using FuzzySharp.PreProcess;
using FuzzySharp.SimilarityRatio.Scorer.Composite;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
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
using SortDirection = DynamicData.Binding.SortDirection;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageFolderCard))]
public partial class ImageFolderCardViewModel : ViewModelBase
{
    private readonly ILogger<ImageFolderCardViewModel> logger;
    private readonly IImageIndexService imageIndexService;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;

    [ObservableProperty]
    private string? searchQuery;

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

        var predicate = this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(50))!
            .Select<PropertyValue<ImageFolderCardViewModel, string>, Func<LocalImageFile, bool>>(
                p => file => SearchPredicate(file, p.Value)
            )
            .AsObservable();

        imageIndexService.InferenceImages.ItemsSource
            .Connect()
            .DeferUntilLoaded()
            .Filter(predicate)
            .SortBy(file => file.LastModifiedAt, SortDirection.Descending)
            .Bind(LocalImages)
            .Subscribe();
    }

    private static bool SearchPredicate(LocalImageFile file, string? query)
    {
        if (
            string.IsNullOrWhiteSpace(query)
            || file.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        // File name
        var filenameScore = Fuzz.WeightedRatio(query, file.FileName, PreprocessMode.Full);
        if (filenameScore > 80)
        {
            return true;
        }

        // Generation params
        if (file.GenerationParameters is { } parameters)
        {
            if (
                parameters.Seed.ToString().StartsWith(query, StringComparison.OrdinalIgnoreCase)
                || parameters.Sampler is { } sampler
                    && sampler.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                || parameters.ModelName is { } modelName
                    && modelName.StartsWith(query, StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        imageIndexService.RefreshIndexForAllCollections().SafeFireAndForget();
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

        var currentIndex = LocalImages.IndexOf(item);

        var image = new ImageSource(new FilePath(imagePath));

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

                        if (newIndex >= 0 && newIndex < LocalImages.Count)
                        {
                            var newImage = LocalImages[newIndex];
                            var newImageSource = new ImageSource(
                                new FilePath(newImage.GetFullPath(settingsManager.ImagesDirectory))
                            );

                            // Preload
                            await newImageSource.GetBitmapAsync();

                            var oldImageSource = sender.ImageSource;

                            sender.ImageSource = newImageSource;
                            sender.LocalImageFile = newImage;

                            // oldImageSource?.Dispose();

                            currentIndex = newIndex;
                        }
                    })
                    .SafeFireAndForget();
            });

        var dialog = new BetterContentDialog
        {
            MaxDialogWidth = 1000,
            MaxDialogHeight = 1000,
            FullSizeDesired = true,
            IsFooterVisible = false,
            CloseOnClickOutside = true,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new ImageViewerDialog
            {
                Width = 1000,
                Height = 1000,
                DataContext = vm
            }
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// Handles clicks to the image delete button
    /// </summary>
    [RelayCommand]
    private async Task OnImageDelete(LocalImageFile? item)
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

        // Remove from index
        imageIndexService.InferenceImages.Remove(item);

        // Invalidate cache
        if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
        {
            loader.RemoveAllNamesFromCache(imageFile.Name);
        }
    }

    /// <summary>
    /// Handles clicks to the image delete button
    /// </summary>
    [RelayCommand]
    private async Task OnImageCopy(LocalImageFile? item)
    {
        if (item?.GetFullPath(settingsManager.ImagesDirectory) is not { } imagePath)
        {
            return;
        }

        var clipboard = App.Clipboard;

        var dataObject = new DataObject();

        // TODO: Not working currently
        dataObject.Set(DataFormats.Files, $"file:///{imagePath}");

        await clipboard.SetDataObjectAsync(dataObject);
    }

    /// <summary>
    /// Handles clicks to the image open-in-explorer button
    /// </summary>
    [RelayCommand]
    private async Task OnImageOpen(LocalImageFile? item)
    {
        if (item?.GetFullPath(settingsManager.ImagesDirectory) is not { } imagePath)
        {
            return;
        }

        await ProcessRunner.OpenFileBrowser(imagePath);
    }

    /// <summary>
    /// Handles clicks to the image export button
    /// </summary>
    private async Task ImageExportImpl(
        LocalImageFile? item,
        SKEncodedImageFormat format,
        bool includeMetadata = false
    )
    {
        if (item?.GetFullPath(settingsManager.ImagesDirectory) is not { } sourcePath)
        {
            return;
        }

        var sourceFile = new FilePath(sourcePath);

        var formatName = format.ToString();

        var storageFile = await App.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Image",
                ShowOverwritePrompt = true,
                SuggestedFileName = item.FileNameWithoutExtension,
                DefaultExtension = formatName.ToLowerInvariant(),
                FileTypeChoices = new FilePickerFileType[]
                {
                    new(formatName)
                    {
                        Patterns = new[] { $"*.{formatName.ToLowerInvariant()}" },
                        MimeTypes = new[] { $"image/{formatName.ToLowerInvariant()}" }
                    }
                }
            }
        );

        if (storageFile?.TryGetLocalPath() is not { } targetPath)
        {
            return;
        }

        var targetFile = new FilePath(targetPath);

        try
        {
            if (format is SKEncodedImageFormat.Png)
            {
                // For include metadata, just copy the file
                if (includeMetadata)
                {
                    await sourceFile.CopyToAsync(targetFile, true);
                }
                else
                {
                    // Otherwise read and strip the metadata
                    var imageBytes = await sourceFile.ReadAllBytesAsync();

                    imageBytes = PngDataHelper.RemoveMetadata(imageBytes);

                    await targetFile.WriteAllBytesAsync(imageBytes);
                }
            }
            else
            {
                await Task.Run(() =>
                {
                    using var fs = sourceFile.Info.OpenRead();
                    var image = SKImage.FromEncodedData(fs);
                    fs.Dispose();

                    using var targetStream = targetFile.Info.OpenWrite();
                    image.Encode(format, 100).SaveTo(targetStream);
                });
            }
        }
        catch (IOException e)
        {
            logger.LogWarning(e, "Failed to export image");
            notificationService.ShowPersistent(
                "Failed to export image",
                e.Message,
                NotificationType.Error
            );
            return;
        }

        notificationService.Show(
            "Image Exported",
            $"Saved to {targetPath}",
            NotificationType.Success
        );
    }

    [RelayCommand]
    private Task OnImageExportPng(LocalImageFile? item) =>
        ImageExportImpl(item, SKEncodedImageFormat.Png);

    [RelayCommand]
    private Task OnImageExportPngWithMetadata(LocalImageFile? item) =>
        ImageExportImpl(item, SKEncodedImageFormat.Png, true);

    [RelayCommand]
    private Task OnImageExportJpeg(LocalImageFile? item) =>
        ImageExportImpl(item, SKEncodedImageFormat.Jpeg);

    [RelayCommand]
    private Task OnImageExportWebp(LocalImageFile? item) =>
        ImageExportImpl(item, SKEncodedImageFormat.Webp);
}
