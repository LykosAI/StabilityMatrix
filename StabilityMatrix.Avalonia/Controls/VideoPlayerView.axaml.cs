using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// In-app player for a video <see cref="ImageSource"/>: looping silent playback plus the route to the
/// system player for audio. Used by the Video data template of the image viewer and the Inference gallery.
/// </summary>
public partial class VideoPlayerView : UserControl
{
    public VideoPlayerView()
    {
        InitializeComponent();
    }

    public static RelayCommand<ImageSource?> OpenExternalCommand { get; } = new(OpenExternal);

    public static AsyncRelayCommand<ImageSource?> CopyFileCommand { get; } = new(CopyFile);

    private static void OpenExternal(ImageSource? imageSource)
    {
        if (imageSource?.LocalFile is not { } videoPath)
            return;

        ProcessRunner.OpenUrl(videoPath.FullPath);
    }

    private static async Task CopyFile(ImageSource? imageSource)
    {
        if (imageSource?.LocalFile is not { } videoPath)
            return;

        await App.Clipboard.SetFileDataObjectAsync(videoPath);
    }
}
