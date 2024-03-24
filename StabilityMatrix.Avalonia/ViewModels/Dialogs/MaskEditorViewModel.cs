using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Controls;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using ContentDialogButton = FluentAvalonia.UI.Controls.ContentDialogButton;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[Transient]
[ManagedService]
[View(typeof(MaskEditorDialog))]
public partial class MaskEditorViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    private bool isMaskEnabled = true;

    /// <summary>
    /// When true, the alpha channel of the image will be used as the mask.
    /// </summary>
    [ObservableProperty]
    private bool useImageAlphaAsMask;

    public PaintCanvasViewModel PaintCanvasViewModel { get; } = new();

    public SKBitmap? BackgroundImage
    {
        get => PaintCanvasViewModel.BackgroundImage;
        set => PaintCanvasViewModel.BackgroundImage = value;
    }

    [RelayCommand]
    private async Task LoadMaskFromFile()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Select a mask image", }
        );

        if (files.Count == 0)
        {
            return;
        }

        await using var stream = await files[0].OpenReadAsync();

        if (PaintCanvasViewModel.LoadCanvasFromImage is { } loadCanvasFromImage)
        {
            await Task.Run(() => loadCanvasFromImage(stream));
        }
    }

    [RelayCommand]
    private async Task SaveMaskToFile()
    {
        var file = await App.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save mask image",
                DefaultExtension = ".webp",
                SuggestedFileName = "mask.webp",
            }
        );

        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync();

        if (PaintCanvasViewModel.SaveCanvasToImage is { } saveCanvasToImage)
        {
            await Task.Run(() => saveCanvasToImage(stream));
        }
    }

    [RelayCommand]
    private async Task ReplaceMaskWithImage() { }

    /// <inheritdoc />
    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        dialog.MaxDialogHeight = 2000;
        dialog.MaxDialogWidth = 2000;
        dialog.ContentMargin = new Thickness(16);
        dialog.FullSizeDesired = true;
        dialog.PrimaryButtonText = Resources.Action_Save;
        dialog.CloseButtonText = Resources.Action_Cancel;
        dialog.DefaultButton = ContentDialogButton.Primary;

        return dialog;
    }
}
