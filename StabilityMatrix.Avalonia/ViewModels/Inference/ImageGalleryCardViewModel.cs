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
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ImageGalleryCard))]
public partial class ImageGalleryCardViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    [ObservableProperty] private bool isPreviewOverlayEnabled;

    [ObservableProperty] private IImage? previewImage;

    [ObservableProperty] private AvaloniaList<string> imageSources = new();
    [ObservableProperty] private string? selectedImage;

    [RelayCommand]
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    // ReSharper disable once UnusedMember.Local
    private async Task FlyoutCopy(IImage? image)
    {
        if (!Compat.IsWindows) return;
        
        if (image is null)
        {
            Logger.Trace("FlyoutCopy: image is null");
            return;
        }
        Logger.Trace($"FlyoutCopy is copying {image}");
        await Task.Run(() => WindowsClipboard.SetBitmap((Bitmap) image));
    }
}
