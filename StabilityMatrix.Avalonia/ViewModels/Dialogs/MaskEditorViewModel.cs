using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Controls;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using ContentDialogButton = FluentAvalonia.UI.Controls.ContentDialogButton;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[RegisterTransient<MaskEditorViewModel>]
[ManagedService]
[View(typeof(MaskEditorDialog))]
public partial class MaskEditorViewModel(ServiceManager<ViewModelBase> vmFactory)
    : LoadableViewModelBase,
        IDisposable
{
    private static FilePickerFileType MaskImageFilePickerType { get; } =
        new("Mask image or json")
        {
            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.json" },
            AppleUniformTypeIdentifiers = new[] { "public.image", "public.json" },
            MimeTypes = new[] { "image/*", "application/json" }
        };

    [JsonIgnore]
    private ImageSource? _cachedMaskRenderInverseAlphaImage;

    [JsonIgnore]
    private ImageSource? _cachedMaskRenderImage;

    /// <summary>
    /// When true, the mask will be applied to the image.
    /// </summary>
    [ObservableProperty]
    private bool isMaskEnabled;

    /// <summary>
    /// When true, the alpha channel of the image will be used as the mask.
    /// </summary>
    [ObservableProperty]
    private bool useImageAlphaAsMask;

    [JsonInclude]
    public PaintCanvasViewModel PaintCanvasViewModel { get; } = vmFactory.Get<PaintCanvasViewModel>();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public ImageSource GetCachedOrNewMaskRenderInverseAlphaImage()
    {
        if (_cachedMaskRenderInverseAlphaImage is null)
        {
            using var skImage = PaintCanvasViewModel.RenderToWhiteChannelImage();

            if (skImage is null)
            {
                throw new InvalidOperationException(
                    "RenderToWhiteChannelImage returned null, BackgroundImageSize likely not set"
                );
            }

            _cachedMaskRenderInverseAlphaImage = new ImageSource(skImage.ToAvaloniaBitmap());
        }

        return _cachedMaskRenderInverseAlphaImage;
    }

    public ImageSource? CachedOrNewMaskRenderImage
    {
        get
        {
            if (_cachedMaskRenderImage is null)
            {
                using var skImage = PaintCanvasViewModel.RenderToImage();

                if (skImage is not null)
                {
                    _cachedMaskRenderImage = new ImageSource(skImage.ToAvaloniaBitmap());
                }
            }

            return _cachedMaskRenderImage;
        }
    }

    public void InvalidateCachedMaskRenderImage()
    {
        _cachedMaskRenderImage?.Dispose();
        _cachedMaskRenderImage = null;

        _cachedMaskRenderInverseAlphaImage?.Dispose();
        _cachedMaskRenderInverseAlphaImage = null;

        OnPropertyChanged(nameof(CachedOrNewMaskRenderImage));
    }

    public BetterContentDialog GetDialog()
    {
        Dispatcher.UIThread.VerifyAccess();

        var dialog = new BetterContentDialog
        {
            Content = this,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxDialogHeight = 2000,
            MaxDialogWidth = 2000,
            ContentMargin = new Thickness(16),
            FullSizeDesired = true,
            PrimaryButtonText = Resources.Action_Save,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary
        };

        return dialog;
    }

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
            var image = PaintCanvasViewModel.RenderToImage();
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

    public override void LoadStateFromJsonObject(JsonObject state)
    {
        base.LoadStateFromJsonObject(state);

        InvalidateCachedMaskRenderImage();
    }

    /*

    public void LoadStateFromJsonObject(JsonObject state)
    {
        var model = state.Deserialize<MaskEditorModel>()!;
        IsMaskEnabled = model.IsMaskEnabled;
        UseImageAlphaAsMask = model.UseImageAlphaAsMask;
        
        if (model.PaintCanvasViewModel is not null)
        {
            PaintCanvasViewModel.LoadStateFromJsonObject(model.PaintCanvasViewModel);
        }
    }

    public JsonObject SaveStateToJsonObject()
    {
        var model = new MaskEditorModel
        {
            IsMaskEnabled = IsMaskEnabled,
            UseImageAlphaAsMask = UseImageAlphaAsMask,
            PaintCanvasViewModel = PaintCanvasViewModel.SaveStateToJsonObject()
        };

        return JsonSerializer.SerializeToNode(model)!.AsObject();
    }

    public record MaskEditorModel
    {
        public bool IsMaskEnabled { get; init; }
        public bool UseImageAlphaAsMask { get; init; }
        public JsonObject? PaintCanvasViewModel { get; init; }
    }*/
    public void Dispose()
    {
        _cachedMaskRenderInverseAlphaImage?.Dispose();
        GC.SuppressFinalize(this);
    }
}
