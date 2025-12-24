using System.Security.Cryptography;
using System.Text;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Service for generating video thumbnails using FFmpeg.
/// </summary>
[RegisterSingleton<IVideoThumbnailService, VideoThumbnailService>]
public class VideoThumbnailService(
    ILogger<VideoThumbnailService> logger,
    IPrerequisiteHelper prerequisiteHelper
) : IVideoThumbnailService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".webm",
        ".mov",
        ".avi",
        ".mkv",
    };

    // Semaphore to prevent multiple concurrent FFmpeg installations
    private static readonly SemaphoreSlim FfmpegInstallLock = new(1, 1);

    public IReadOnlySet<string> SupportedVideoExtensions => VideoExtensions;

    /// <inheritdoc />
    public bool IsVideoFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return VideoExtensions.Contains(extension);
    }

    /// <inheritdoc />
    public async Task EnsureFfmpegInstalledAsync(IProgress<ProgressReport>? progress = null)
    {
        if (!prerequisiteHelper.IsFfmpegInstalled)
        {
            await prerequisiteHelper.InstallFfmpegIfNecessary(progress).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public string? GetExistingThumbnailPath(string videoPath)
    {
        if (!File.Exists(videoPath))
        {
            return null;
        }

        var videoDir = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrEmpty(videoDir))
        {
            return null;
        }

        var thumbnailsDir = Path.Combine(videoDir, ".sm-thumbs");
        var thumbnailName = GetThumbnailName(videoPath);
        var thumbnailPath = Path.Combine(thumbnailsDir, thumbnailName);

        return File.Exists(thumbnailPath) ? thumbnailPath : null;
    }

    /// <inheritdoc />
    public async Task<string?> GetOrCreateThumbnailAsync(
        string videoPath,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(videoPath))
        {
            logger.LogWarning("Video file not found: {VideoPath}", videoPath);
            return null;
        }

        // Ensure FFmpeg is available - install if needed
        if (!prerequisiteHelper.IsFfmpegInstalled)
        {
            // Use semaphore to prevent multiple concurrent install attempts
            await FfmpegInstallLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (!prerequisiteHelper.IsFfmpegInstalled)
                {
                    logger.LogInformation("FFmpeg not installed, downloading...");
                    await prerequisiteHelper.InstallFfmpegIfNecessary().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to install FFmpeg");
                return null;
            }
            finally
            {
                FfmpegInstallLock.Release();
            }

            // Verify installation succeeded
            if (!prerequisiteHelper.IsFfmpegInstalled)
            {
                logger.LogWarning("FFmpeg installation failed");
                return null;
            }
        }

        // Get the directory containing the video
        var videoDir = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrEmpty(videoDir))
        {
            return null;
        }

        // Create thumbnails directory if needed (use distinct name to avoid WebUI conflicts)
        var thumbnailsDir = Path.Combine(videoDir, ".sm-thumbs");
        Directory.CreateDirectory(thumbnailsDir);

        // Generate a unique thumbnail name based on video filename and size
        var thumbnailName = GetThumbnailName(videoPath);
        var thumbnailPath = Path.Combine(thumbnailsDir, thumbnailName);

        // Return existing thumbnail if it exists
        if (File.Exists(thumbnailPath))
        {
            return thumbnailPath;
        }

        // Generate thumbnail using FFmpeg
        try
        {
            var result = await GenerateThumbnailAsync(videoPath, thumbnailPath, cancellationToken)
                .ConfigureAwait(false);

            return result ? thumbnailPath : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate thumbnail for {VideoPath}", videoPath);
            return null;
        }
    }

    private string GetThumbnailName(string videoPath)
    {
        // Create a hash based on full path and file size for uniqueness
        // Use only the hash for the filename to avoid MAX_PATH issues with long video names
        var fileInfo = new FileInfo(videoPath);
        var hashInput = $"{videoPath}_{fileInfo.Length}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hashString = Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars

        return $"thumb_{hashString}.jpg";
    }

    private async Task<bool> GenerateThumbnailAsync(
        string videoPath,
        string thumbnailPath,
        CancellationToken cancellationToken
    )
    {
        var ffmpegPath = prerequisiteHelper.FfmpegPath;

        // FFmpeg command to extract first frame as JPEG
        // -nostdin: don't wait for stdin (prevents hanging)
        // -loglevel error: reduce output to prevent pipe buffer issues
        // -hide_banner: don't print version info
        // -y: overwrite output
        // -i: input file
        // -vframes 1: extract only 1 frame
        // -q:v 2: quality (2 is high quality)
        // -vf "scale=300:-1": scale to 300px width, maintain aspect ratio
        var args = new ProcessArgsBuilder()
            .AddArg("-nostdin")
            .AddArg("-loglevel")
            .AddArg("error")
            .AddArg("-hide_banner")
            .AddArg("-y")
            .AddArg("-i")
            .AddArg(videoPath)
            .AddArg("-vframes")
            .AddArg("1")
            .AddArg("-q:v")
            .AddArg("2")
            .AddArg("-vf")
            .AddArg("scale=300:-1")
            .AddArg(thumbnailPath);

        logger.LogInformation("Running FFmpeg: {FfmpegPath} {Args}", ffmpegPath, args);

        try
        {
            var result = await ProcessRunner
                .GetProcessResultAsync(ffmpegPath, args.ToProcessArgs())
                .ConfigureAwait(false);

            logger.LogInformation(
                "FFmpeg completed for {Video}: ExitCode={ExitCode}",
                Path.GetFileName(videoPath),
                result.ExitCode
            );

            if (result.ExitCode != 0)
            {
                logger.LogWarning(
                    "FFmpeg exited with code {ExitCode}: {StdErr}",
                    result.ExitCode,
                    result.StandardError
                );
                return false;
            }

            var exists = File.Exists(thumbnailPath);
            logger.LogInformation(
                "Thumbnail created: {ThumbnailPath}, Exists: {Exists}",
                thumbnailPath,
                exists
            );
            return exists;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FFmpeg process failed for {Video}", videoPath);
            throw;
        }
    }
}
