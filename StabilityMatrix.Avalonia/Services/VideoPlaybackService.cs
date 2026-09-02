using System;
using System.Threading.Tasks;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<IVideoPlaybackService, VideoPlaybackService>]
public class VideoPlaybackService(
    ILogger<VideoPlaybackService> logger,
    IPrerequisiteHelper prerequisiteHelper,
    IVideoThumbnailService videoThumbnailService
) : IVideoPlaybackService
{
    /// <inheritdoc />
    public async Task<bool> EnsureAvailableAsync()
    {
        if (prerequisiteHelper.IsFfmpegInstalled)
            return true;

        try
        {
            await videoThumbnailService.EnsureFfmpegInstalledAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to install ffmpeg for in-app video playback");
        }

        return prerequisiteHelper.IsFfmpegInstalled;
    }

    /// <inheritdoc />
    public void OpenExternally(string videoPath)
    {
        ProcessRunner.OpenUrl(videoPath);
    }
}
