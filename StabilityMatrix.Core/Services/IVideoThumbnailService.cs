using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Service for generating video thumbnails using FFmpeg.
/// </summary>
public interface IVideoThumbnailService
{
    /// <summary>
    /// Supported video file extensions.
    /// </summary>
    IReadOnlySet<string> SupportedVideoExtensions { get; }

    /// <summary>
    /// Check if a file path represents a video file based on extension.
    /// </summary>
    bool IsVideoFile(string filePath);

    /// <summary>
    /// Get or create a thumbnail for a video file.
    /// </summary>
    /// <param name="videoPath">Absolute path to the video file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the thumbnail image, or null if generation failed.</returns>
    Task<string?> GetOrCreateThumbnailAsync(string videoPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the thumbnail path if it already exists (synchronous, no generation).
    /// Use this for initial display to avoid async delays.
    /// </summary>
    /// <param name="videoPath">Absolute path to the video file.</param>
    /// <returns>Absolute path to the thumbnail if it exists, otherwise null.</returns>
    string? GetExistingThumbnailPath(string videoPath);

    /// <summary>
    /// Ensures FFmpeg is installed before using the service.
    /// </summary>
    Task EnsureFfmpegInstalledAsync(IProgress<ProgressReport>? progress = null);
}
