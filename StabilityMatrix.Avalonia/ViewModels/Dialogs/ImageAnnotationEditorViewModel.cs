using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
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

/// <summary>
/// ViewModel for the image annotation editor dialog.
/// Allows users to draw/annotate on images before sending to AI providers.
/// </summary>
[RegisterTransient<ImageAnnotationEditorViewModel>]
[ManagedService]
[View(typeof(ImageAnnotationEditorDialog))]
public partial class ImageAnnotationEditorViewModel(IServiceManager<ViewModelBase> vmFactory)
    : LoadableViewModelBase,
        IDisposable
{
    [JsonIgnore]
    private SKBitmap? originalBitmap;

    [JsonIgnore]
    private ImageSource? cachedAnnotatedImage;

    /// <summary>
    /// The source image file path being edited
    /// </summary>
    [ObservableProperty]
    private string? sourceFilePath;

    /// <summary>
    /// The paint canvas view model for drawing annotations
    /// </summary>
    [JsonInclude]
    public PaintCanvasViewModel PaintCanvasViewModel { get; } = vmFactory.Get<PaintCanvasViewModel>();

    /// <summary>
    /// Whether there are any annotations on the canvas
    /// </summary>
    public bool HasAnnotations => PaintCanvasViewModel.Paths.Count > 0;

    /// <summary>
    /// Load an image from file path for editing
    /// </summary>
    public void LoadImage(string filePath)
    {
        SourceFilePath = filePath;
        originalBitmap?.Dispose();
        originalBitmap = SKBitmap.Decode(filePath);

        if (originalBitmap != null)
        {
            PaintCanvasViewModel.BackgroundImage = originalBitmap;
            PaintCanvasViewModel.RefreshCanvas?.Invoke();
        }
    }

    /// <summary>
    /// Load an image from bitmap for editing
    /// </summary>
    public void LoadImage(Bitmap bitmap, string? sourcePath = null)
    {
        SourceFilePath = sourcePath;
        originalBitmap?.Dispose();

        // Convert Avalonia Bitmap to SKBitmap
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        originalBitmap = SKBitmap.Decode(stream);

        if (originalBitmap != null)
        {
            PaintCanvasViewModel.BackgroundImage = originalBitmap;
            PaintCanvasViewModel.RefreshCanvas?.Invoke();
        }
    }

    /// <summary>
    /// Get the annotated image with drawings overlaid on the original
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public ImageSource? GetAnnotatedImage()
    {
        if (cachedAnnotatedImage != null)
        {
            return cachedAnnotatedImage;
        }

        using var skImage = RenderAnnotatedImage();
        if (skImage == null)
        {
            return null;
        }

        cachedAnnotatedImage = new ImageSource(skImage.ToAvaloniaBitmap());
        return cachedAnnotatedImage;
    }

    /// <summary>
    /// Render the annotated image to an SKImage
    /// </summary>
    public SKImage? RenderAnnotatedImage()
    {
        var canvasSize = PaintCanvasViewModel.CanvasSize;
        if (canvasSize.IsEmpty)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(canvasSize.Width, canvasSize.Height));
        PaintCanvasViewModel.RenderToSurface(
            surface,
            renderBackgroundFill: false,
            renderBackgroundImage: true
        );

        return surface.Snapshot();
    }

    /// <summary>
    /// Save the annotated image to a file
    /// </summary>
    public async Task<string?> SaveAnnotatedImageAsync(string? targetPath = null)
    {
        using var image = RenderAnnotatedImage();
        if (image == null)
        {
            return null;
        }

        // Generate target path if not provided
        targetPath ??= Path.Combine(Path.GetTempPath(), $"annotated_{Guid.NewGuid():N}.png");

        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        await using var fileStream = File.OpenWrite(targetPath);
        data.SaveTo(fileStream);

        return targetPath;
    }

    /// <summary>
    /// Get the annotated image as a byte array (PNG format)
    /// </summary>
    public byte[]? GetAnnotatedImageBytes()
    {
        using var image = RenderAnnotatedImage();
        if (image == null)
        {
            return null;
        }

        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Invalidate the cached annotated image
    /// </summary>
    public void InvalidateCache()
    {
        cachedAnnotatedImage?.Dispose();
        cachedAnnotatedImage = null;
    }

    /// <summary>
    /// Clear all annotations from the canvas
    /// </summary>
    [RelayCommand]
    public void ClearAnnotations()
    {
        PaintCanvasViewModel.Paths = [];
        PaintCanvasViewModel.RefreshCanvas?.Invoke();
        InvalidateCache();
    }

    /// <summary>
    /// Create and show the editor dialog
    /// </summary>
    public BetterContentDialog GetDialog()
    {
        Dispatcher.UIThread.VerifyAccess();

        var dialog = new BetterContentDialog
        {
            Content = this,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxDialogHeight = 900,
            MaxDialogWidth = 1200,
            ContentMargin = new Thickness(16),
            FullSizeDesired = true,
            PrimaryButtonText = Resources.Action_Save,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        return dialog;
    }

    public void Dispose()
    {
        originalBitmap?.Dispose();
        cachedAnnotatedImage?.Dispose();
        GC.SuppressFinalize(this);
    }
}
