using System;
using System.IO;
using System.Text.Json;
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
    /// <summary>
    /// When true, the mask will be applied to the image.
    /// </summary>
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

    public static FilePickerFileType MaskImageFilePickerType { get; } =
        new("Mask image or json")
        {
            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.json" },
            AppleUniformTypeIdentifiers = new[] { "public.image", "public.json" },
            MimeTypes = new[] { "image/*", "application/json" }
        };

    [RelayCommand]
    private async Task DebugSelectFileLoadMask()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Select a mask", FileTypeFilter = [MaskImageFilePickerType] }
        );

        if (files.Count == 0)
        {
            return;
        }

        var file = files[0];
        await using var stream = await file.OpenReadAsync();

        if (file.Name.EndsWith(".json"))
        {
            var json = await JsonSerializer.DeserializeAsync<JsonObject>(stream);
            PaintCanvasViewModel.LoadStateFromJsonObject(json!);
        }
        else
        {
            var bitmap = SKBitmap.Decode(stream);
            PaintCanvasViewModel.LoadCanvasFromBitmap(bitmap);
        }
    }

    [RelayCommand]
    private async Task DebugSelectFileSaveMask()
    {
        var file = await App.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save mask image",
                DefaultExtension = ".json",
                FileTypeChoices = [MaskImageFilePickerType],
                SuggestedFileName = "mask.json",
            }
        );

        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync();

        if (file.Name.EndsWith(".json"))
        {
            var json = PaintCanvasViewModel.SaveStateToJsonObject();
            await JsonSerializer.SerializeAsync(stream, json);
        }
        else
        {
            var image = PaintCanvasViewModel.GetCanvasSnapshot?.Invoke();
            await image!
                .Encode(
                    Path.GetExtension(file.Name.ToLowerInvariant()) switch
                    {
                        ".png" => SKEncodedImageFormat.Png,
                        ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                        ".webp" => SKEncodedImageFormat.Webp,
                        _ => throw new NotSupportedException("Unsupported image format")
                    },
                    100
                )
                .AsStream()
                .CopyToAsync(stream);
        }
    }

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
