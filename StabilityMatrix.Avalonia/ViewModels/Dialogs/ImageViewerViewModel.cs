using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Core;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;
using Size = StabilityMatrix.Core.Helper.Size;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ImageViewerDialog))]
[ManagedService]
[Transient]
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
        NavigationRequested?.Invoke(this, DirectionalNavigationEventArgs.Down);
    }

    [RelayCommand]
    private void OnNavigatePrevious()
    {
        NavigationRequested?.Invoke(this, DirectionalNavigationEventArgs.Up);
    }

    [RelayCommand]
    private async Task CopyImage(Bitmap? image)
    {
        if (image is null || !Compat.IsWindows)
            return;

        await Task.Run(() =>
        {
            if (Compat.IsWindows)
            {
                WindowsClipboard.SetBitmap(image);
            }
        });
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
                DataContext = this
            }
        };

        return dialog;
    }
}
