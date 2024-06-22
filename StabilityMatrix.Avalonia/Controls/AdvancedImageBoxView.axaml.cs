using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Controls;

public partial class AdvancedImageBoxView : UserControl
{
    public AdvancedImageBoxView()
    {
        InitializeComponent();
    }

    public static AsyncRelayCommand<ImageSource?> FlyoutCopyCommand { get; } = new(FlyoutCopy);

    private static async Task FlyoutCopy(ImageSource? imageSource)
    {
        if (imageSource is null)
            return;

        var settings = App.Services.GetService<ISettingsManager>()?.Settings;

        if (Compat.IsWindows && settings?.AlwaysCopyImagesAsFiles != true && imageSource.Bitmap is { } bitmap)
        {
            // Use bitmap on Windows if available
            await WindowsClipboard.SetBitmapAsync(bitmap);
        }
        else if (imageSource.LocalFile is { } imagePath)
        {
            // Other OS or no bitmap, use image source
            var clipboard = App.Clipboard;
            await clipboard.SetFileDataObjectAsync(imagePath);
        }
    }
}
