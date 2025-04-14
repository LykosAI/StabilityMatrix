using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FuzzySharp;
using FuzzySharp.PreProcess;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Native;
using SortDirection = DynamicData.Binding.SortDirection;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageFolderCard))]
[ManagedService]
[RegisterScoped<ImageFolderCardViewModel>]
public partial class ImageFolderCardViewModel : DisposableViewModelBase
{
    private readonly ILogger<ImageFolderCardViewModel> logger;
    private readonly IImageIndexService imageIndexService;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly IServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    private string? searchQuery;

    [ObservableProperty]
    private Size imageSize = new(150, 190);

    /// <summary>
    /// Collection of local image files
    /// </summary>
    public IObservableCollection<LocalImageFile> LocalImages { get; } =
        new ObservableCollectionExtended<LocalImageFile>();

    public ImageFolderCardViewModel(
        ILogger<ImageFolderCardViewModel> logger,
        IImageIndexService imageIndexService,
        ISettingsManager settingsManager,
        INotificationService notificationService,
        IServiceManager<ViewModelBase> vmFactory
    )
    {
        this.logger = logger;
        this.imageIndexService = imageIndexService;
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;

        var searcher = new ImageSearcher();

        // Observable predicate from SearchQuery changes
        var searchPredicate = this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(50))!
            .Select(property => searcher.GetPredicate(property.Value))
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        imageIndexService
            .InferenceImages.ItemsSource.Connect()
            .DeferUntilLoaded()
            .Filter(searchPredicate)
            .SortBy(file => file.LastModifiedAt, SortDirection.Descending)
            .Bind(LocalImages)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.ImageSize,
                settings => settings.InferenceImageSize,
                delay: TimeSpan.FromMilliseconds(250)
            )
        );
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
        ImageSize = settingsManager.Settings.InferenceImageSize;
        imageIndexService.RefreshIndexForAllCollections().SafeFireAndForget();
    }

    /// <summary>
    /// Gets the image path if it exists, returns null.
    /// If the image path is resolved but the file doesn't exist, it will be removed from the index.
    /// </summary>
    private FilePath? GetImagePathIfExists(LocalImageFile item)
    {
        var imageFile = new FilePath(item.AbsolutePath);

        if (!imageFile.Exists)
        {
            // Remove from index
            imageIndexService.InferenceImages.Remove(item);

            // Invalidate cache
            if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
            {
                loader.RemoveAllNamesFromCache(imageFile.Name);
            }

            return null;
        }

        return imageFile;
    }

    /// <summary>
    /// Handles image clicks to show preview
    /// </summary>
    [RelayCommand]
    private async Task OnImageClick(LocalImageFile item)
    {
        if (GetImagePathIfExists(item) is not { } imageFile)
        {
            return;
        }

        var currentIndex = LocalImages.IndexOf(item);

        var image = new ImageSource(imageFile);

        // Preload
        await image.GetBitmapAsync();

        var vm = vmFactory.Get<ImageViewerViewModel>();
        vm.ImageSource = image;
        vm.LocalImageFile = item;

        using var onNext = Observable
            .FromEventPattern<DirectionalNavigationEventArgs>(
                vm,
                nameof(ImageViewerViewModel.NavigationRequested)
            )
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(ctx =>
            {
                Dispatcher
                    .UIThread.InvokeAsync(async () =>
                    {
                        var sender = (ImageViewerViewModel)ctx.Sender!;
                        var newIndex = currentIndex + (ctx.EventArgs.IsNext ? 1 : -1);

                        if (newIndex >= 0 && newIndex < LocalImages.Count)
                        {
                            var newImage = LocalImages[newIndex];
                            var newImageSource = new ImageSource(newImage.AbsolutePath);

                            // Preload
                            await newImageSource.GetBitmapAsync();

                            // var oldImageSource = sender.ImageSource;

                            sender.ImageSource = newImageSource;
                            sender.LocalImageFile = newImage;

                            // oldImageSource?.Dispose();

                            currentIndex = newIndex;
                        }
                    })
                    .SafeFireAndForget();
            });

        await vm.GetDialog().ShowAsync();
    }

    /// <summary>
    /// Handles clicks to the image delete button
    /// </summary>
    [RelayCommand]
    private async Task OnImageDelete(LocalImageFile? item)
    {
        if (item is null || GetImagePathIfExists(item) is not { } imageFile)
        {
            return;
        }

        // Delete the file
        var isRecycle =
            settingsManager.Settings.IsInferenceImageBrowserUseRecycleBinForDelete
            && NativeFileOperations.IsRecycleBinAvailable;

        var result = isRecycle
            ? await notificationService.TryAsync(
                NativeFileOperations.RecycleBin!.MoveFileToRecycleBinAsync(imageFile)
            )
            : await notificationService.TryAsync(imageFile.DeleteAsync());

        if (result.IsSuccessful)
        {
            // Remove from index
            imageIndexService.InferenceImages.Remove(item);

            // Invalidate cache
            if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
            {
                loader.RemoveAllNamesFromCache(imageFile.Name);
            }
        }
        else
        {
            logger.LogWarning(result.Exception, "Failed to delete image");
        }
    }

    /// <summary>
    /// Handles clicks to the image delete button
    /// </summary>
    [RelayCommand]
    private async Task OnImageCopy(LocalImageFile? item)
    {
        if (item is null || GetImagePathIfExists(item) is not { } imageFile)
        {
            return;
        }

        var clipboard = App.Clipboard;

        await clipboard.SetFileDataObjectAsync(imageFile.FullPath);
    }

    /// <summary>
    /// Handles clicks to the image open-in-explorer button
    /// </summary>
    [RelayCommand]
    private async Task OnImageOpen(LocalImageFile? item)
    {
        if (item is null || GetImagePathIfExists(item) is not { } imageFile)
        {
            return;
        }

        await ProcessRunner.OpenFileBrowser(imageFile);
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
        if (item is null || GetImagePathIfExists(item) is not { } sourceFile)
        {
            return;
        }

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
            notificationService.ShowPersistent("Failed to export image", e.Message, NotificationType.Error);
            return;
        }

        notificationService.Show("Image Exported", $"Saved to {targetPath}", NotificationType.Success);
    }

    [RelayCommand]
    private async Task CopySeedToClipboard(LocalImageFile? item)
    {
        if (item?.GenerationParameters is null || App.Clipboard is null)
        {
            return;
        }

        await App.Clipboard.SetTextAsync(item.GenerationParameters.Seed.ToString());
    }

    [RelayCommand]
    private async Task CopyPromptToClipboard(LocalImageFile? item)
    {
        if (item?.GenerationParameters is null || App.Clipboard is null)
        {
            return;
        }

        await App.Clipboard.SetTextAsync(item.GenerationParameters.PositivePrompt);
    }

    [RelayCommand]
    private async Task CopyNegativePromptToClipboard(LocalImageFile? item)
    {
        if (item?.GenerationParameters is null || App.Clipboard is null)
        {
            return;
        }

        await App.Clipboard.SetTextAsync(item.GenerationParameters.NegativePrompt);
    }

    [RelayCommand]
    private async Task CopyModelNameToClipboard(LocalImageFile? item)
    {
        if (item?.GenerationParameters is null || App.Clipboard is null)
        {
            return;
        }

        await App.Clipboard.SetTextAsync(item.GenerationParameters.ModelName);
    }

    [RelayCommand]
    private async Task CopyModelHashToClipboard(LocalImageFile? item)
    {
        if (item?.GenerationParameters is null || App.Clipboard is null)
        {
            return;
        }

        await App.Clipboard.SetTextAsync(item.GenerationParameters.ModelHash);
    }

    [RelayCommand]
    private Task OnImageExportPng(LocalImageFile? item) => ImageExportImpl(item, SKEncodedImageFormat.Png);

    [RelayCommand]
    private Task OnImageExportPngWithMetadata(LocalImageFile? item) =>
        ImageExportImpl(item, SKEncodedImageFormat.Png, true);

    [RelayCommand]
    private Task OnImageExportJpeg(LocalImageFile? item) => ImageExportImpl(item, SKEncodedImageFormat.Jpeg);

    [RelayCommand]
    private Task OnImageExportWebp(LocalImageFile? item) => ImageExportImpl(item, SKEncodedImageFormat.Webp);
}
