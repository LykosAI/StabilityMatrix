using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Core;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;
using Path = System.IO.Path;
using Size = StabilityMatrix.Core.Helper.Size;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ImageViewerDialog))]
[ManagedService]
[RegisterTransient<ImageViewerViewModel>]
public partial class ImageViewerViewModel(
    ILogger<ImageViewerViewModel> logger,
    ISettingsManager settingsManager
) : ContentDialogViewModelBase
{
    [ObservableProperty]
    private ImageSource? imageSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocalGenerationParameters))]
    private LocalImageFile? localImageFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocalGenerationParameters))]
    public partial CivitImageGenerationDataResponse? CivitImageMetadata { get; set; }

    [ObservableProperty]
    private bool isFooterEnabled;

    [ObservableProperty]
    private string? fileNameText;

    [ObservableProperty]
    private string? fileSizeText;

    [ObservableProperty]
    private string? imageSizeText;

    /// <summary>
    /// Whether local generation parameters are available.
    /// </summary>
    public bool HasLocalGenerationParameters => LocalImageFile?.GenerationParameters is not null;

    /// <summary>
    /// Whether Civitai image metadata is available.
    /// </summary>
    public bool HasCivitImageMetadata => CivitImageMetadata is not null;

    public event EventHandler<DirectionalNavigationEventArgs>? NavigationRequested;

    partial void OnLocalImageFileChanged(LocalImageFile? value)
    {
        if (value?.ImageSize is { IsEmpty: false } size)
        {
            ImageSizeText = $"{size.Width} x {size.Height}";
        }
    }

    partial void OnImageSourceChanged(ImageSource? value)
    {
        if (value?.LocalFile is { Exists: true } localFile)
        {
            FileNameText = localFile.Name;
            FileSizeText = Size.FormatBase10Bytes(localFile.GetSize(true));
        }
    }

    partial void OnCivitImageMetadataChanged(CivitImageGenerationDataResponse? value)
    {
        if (value is null)
            return;

        ImageSizeText = value.Metadata?.Dimensions ?? string.Empty;
    }

    [RelayCommand]
    private void OnNavigateNext()
    {
        NavigationRequested?.Invoke(this, DirectionalNavigationEventArgs.Down);
    }

    [RelayCommand]
    private void OnNavigatePrevious()
    {
        NavigationRequested?.Invoke(this, DirectionalNavigationEventArgs.Up);
    }

    [RelayCommand]
    private async Task CopyImage(ImageSource? image)
    {
        if (image is null)
            return;

        if (image.LocalFile is { } imagePath)
        {
            await App.Clipboard.SetFileDataObjectAsync(imagePath);
        }
        else if (await image.GetBitmapAsync() is { } bitmap)
        {
            // Write to temp file
            var tempFile = new FilePath(Path.GetTempFileName() + ".png");

            bitmap.Save(tempFile);

            await App.Clipboard.SetFileDataObjectAsync(tempFile);
        }
        else
        {
            logger.LogWarning("Failed to copy image, no file path or bitmap: {Image}", image);
        }
    }

    [RelayCommand]
    private async Task CopyImageAsBitmap(ImageSource? image)
    {
        if (image is null || !Compat.IsWindows)
            return;

        if (await image.GetBitmapAsync() is { } bitmap)
        {
            await WindowsClipboard.SetBitmapAsync(bitmap);
        }
        else
        {
            logger.LogWarning("Failed to copy image, no bitmap: {Image}", image);
        }
    }

    [RelayCommand]
    private async Task CopyThingToClipboard(object? thing)
    {
        if (thing is null)
            return;

        await App.Clipboard.SetTextAsync(thing.ToString());
    }

    public override BetterContentDialog GetDialog()
    {
        var margins = new Thickness(64, 32);

        var mainWindowSize = App.Services.GetService<MainWindow>()?.ClientSize;
        var dialogSize = new global::Avalonia.Size(
            Math.Floor((mainWindowSize?.Width * 0.6 ?? 1000) - margins.Horizontal()),
            Math.Floor((mainWindowSize?.Height ?? 1000) - margins.Vertical())
        );

        var dialog = new BetterContentDialog
        {
            MaxDialogWidth = dialogSize.Width,
            MaxDialogHeight = dialogSize.Height,
            ContentMargin = margins,
            FullSizeDesired = true,
            IsFooterVisible = false,
            CloseOnClickOutside = true,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new ImageViewerDialog
            {
                Width = dialogSize.Width,
                Height = dialogSize.Height,
                DataContext = this,
            },
        };

        return dialog;
    }
}
