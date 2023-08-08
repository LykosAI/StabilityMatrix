using System;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageGalleryCard))]
public partial class ImageGalleryCardViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

    public bool CanNavigateBack => SelectedImageIndex > 0;
    public bool CanNavigateForward => SelectedImageIndex < ImageSources.Count - 1;

    public ImageGalleryCardViewModel()
    {
        ImageSources.CollectionChanged += OnImageSourcesItemsChanged;
    }
    
    private void OnImageSourcesItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is AvaloniaList<ImageSource> sources)
        {
            if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove
                or NotifyCollectionChangedAction.Reset)
            {
                if (sources.Count == 0)
                {
                    SelectedImageIndex = -1;
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

        Logger.Trace($"FlyoutCopy is copying {image}");

        await Task.Run(() =>
        {
            if (Compat.IsWindows)
            {
                WindowsClipboard.SetBitmap((Bitmap)image);
            }
        });
    }
}
