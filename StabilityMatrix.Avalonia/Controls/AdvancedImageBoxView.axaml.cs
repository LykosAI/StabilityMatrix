using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Controls;

public partial class AdvancedImageBoxView : UserControl
{
    public AdvancedImageBoxView()
    {
        InitializeComponent();
    }

    public static AsyncRelayCommand<ImageSource?> FlyoutCopyCommand { get; } = new(FlyoutCopy);

    public static AsyncRelayCommand<ImageSource?> FlyoutCopyAsBitmapCommand { get; } =
        new(FlyoutCopyAsBitmap);

    private static async Task FlyoutCopy(ImageSource? imageSource)
    {
        if (imageSource is null)
            return;

        if (imageSource.LocalFile is { } imagePath)
        {
            await App.Clipboard.SetFileDataObjectAsync(imagePath);
        }
        else if (await imageSource.GetBitmapAsync() is { } bitmap)
        {
            // Write to temp file
            var tempFile = new FilePath(Path.GetTempFileName() + ".png");

            bitmap.Save(tempFile);

            await App.Clipboard.SetFileDataObjectAsync(tempFile);
        }
    }

    private static async Task FlyoutCopyAsBitmap(ImageSource? imageSource)
    {
        if (imageSource is null || !Compat.IsWindows)
            return;

        if (await imageSource.GetBitmapAsync() is { } bitmap)
        {
            await WindowsClipboard.SetBitmapAsync(bitmap);
        }
    }
}
