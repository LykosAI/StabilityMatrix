using Avalonia.Collections;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageGalleryCard))]
public partial class ImageGalleryCardViewModel : ViewModelBase
{
    [ObservableProperty] private bool isPreviewOverlayEnabled;

    [ObservableProperty] private IImage? previewImage;

    [ObservableProperty] private AvaloniaList<string> imageSources = new();
}
