using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageGalleryCard))]
[ManagedService]
[RegisterTransient<ImageGalleryCardViewModel>]
public partial class ImageGalleryCardViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    private bool isPreviewOverlayEnabled;

    [ObservableProperty]
    private Bitmap? previewImage;

    [ObservableProperty]
    private AvaloniaList<ImageSource> imageSources = new();

    [ObservableProperty]
    private ImageSource? selectedImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigateBack), nameof(CanNavigateForward))]
    private int selectedImageIndex;

    [ObservableProperty]
    private bool isPixelGridEnabled;

    public bool HasMultipleImages => ImageSources.Count > 1;

    public bool CanNavigateBack => SelectedImageIndex > 0;
    public bool CanNavigateForward => SelectedImageIndex < ImageSources.Count - 1;

    public ImageGalleryCardViewModel(
        IServiceManager<ViewModelBase> vmFactory,
        ISettingsManager settingsManager
    )
    {
        this.vmFactory = vmFactory;

        IsPixelGridEnabled = settingsManager.Settings.IsImageViewerPixelGridEnabled;

        settingsManager.RegisterPropertyChangedHandler(
            s => s.IsImageViewerPixelGridEnabled,
            newValue =>
            {
                IsPixelGridEnabled = newValue;
            }
        );

        ImageSources.CollectionChanged += OnImageSourcesItemsChanged;
    }

    public void SetPreviewImage(byte[] imageBytes)
    {
        Dispatcher.UIThread.Post(() =>
        {
            using var stream = new MemoryStream(imageBytes);

            var bitmap = new Bitmap(stream);

            var currentImage = PreviewImage;

            PreviewImage = bitmap;
            IsPreviewOverlayEnabled = true;

            // currentImage?.Dispose();
        });
    }

    private void OnImageSourcesItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is AvaloniaList<ImageSource> sources)
        {
            if (
                e.Action
                is NotifyCollectionChangedAction.Add
                    or NotifyCollectionChangedAction.Remove
                    or NotifyCollectionChangedAction.Reset
            )
            {
                if (sources.Count == 0)
                {
                    SelectedImageIndex = 0;
                }
                else if (SelectedImageIndex == -1)
                {
                    SelectedImageIndex = 0;
                }
                // Clamp the selected index to the new range
                else
                {
                    SelectedImageIndex = Math.Clamp(SelectedImageIndex, 0, sources.Count - 1);
                }
                OnPropertyChanged(nameof(CanNavigateBack));
                OnPropertyChanged(nameof(CanNavigateForward));
                OnPropertyChanged(nameof(HasMultipleImages));
            }
        }
    }

    [RelayCommand]
    // ReSharper disable once UnusedMember.Local
    private async Task FlyoutCopy(IImage? image)
    {
        if (image is null)
        {
            Logger.Trace("FlyoutCopy: image is null");
            return;
        }

        Logger.Trace($"FlyoutCopy is copying bitmap...");

        await Task.Run(() =>
        {
            if (Compat.IsWindows)
            {
                WindowsClipboard.SetBitmap((Bitmap)image);
            }
        });
    }

    [RelayCommand]
    // ReSharper disable once UnusedMember.Local
    private async Task FlyoutPreview(IImage? image)
    {
        if (image is null)
        {
            Logger.Trace("FlyoutPreview: image is null");
            return;
        }

        Logger.Trace($"FlyoutPreview opening...");

        var viewerVm = vmFactory.Get<ImageViewerViewModel>();
        viewerVm.ImageSource = new ImageSource((Bitmap)image);

        var dialog = new BetterContentDialog { Content = new ImageViewerDialog { DataContext = viewerVm, } };

        await dialog.ShowAsync();
    }
}
