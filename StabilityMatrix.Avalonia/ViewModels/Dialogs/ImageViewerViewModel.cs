using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ImageViewerDialog))]
public partial class ImageViewerViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    private Bitmap? image;

    [ObservableProperty]
    private string? source;
}
