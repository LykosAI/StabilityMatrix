using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ImageViewerDialog))]
public partial class ImageViewerViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    private ImageSource? imageSource;

    [ObservableProperty]
    private LocalImageFile? localImageFile;

    [ObservableProperty]
    private bool isFooterEnabled;

    [ObservableProperty]
    private string? fileNameText;

    [ObservableProperty]
    private string? fileSizeText;

    [ObservableProperty]
    private string? imageSizeText;

    partial void OnLocalImageFileChanged(LocalImageFile? value)
    {
        if (value?.ImageSize is { IsEmpty: false } size)
        {
            ImageSizeText = $"{size.Width} x {size.Height}";
        }
    }

    partial void OnImageSourceChanged(ImageSource? value)
    {
        if (value?.LocalFile is { } localFile)
        {
            FileNameText = localFile.Name;
            FileSizeText = Size.FormatBase10Bytes(localFile.GetSize(true));
        }
    }
}
