using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [NotifyPropertyChangedFor(nameof(HasGenerationParameters))]
    private LocalImageFile? localImageFile;

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
    public bool HasGenerationParameters => LocalImageFile?.GenerationParameters is not null;

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
        if (value?.LocalFile is { } localFile)
        {
            FileNameText = localFile.Name;
            FileSizeText = Size.FormatBase10Bytes(localFile.GetSize(true));
        }
    }

    [RelayCommand]
    private void OnNavigateNext()
    {
        NavigationRequested?.Invoke(this, DirectionalNavigationEventArgs.Up);
    }

    [RelayCommand]
    private void OnNavigatePrevious()
    {
        NavigationRequested?.Invoke(this, DirectionalNavigationEventArgs.Down);
    }
}
