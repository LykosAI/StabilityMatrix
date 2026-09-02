using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Decides whether a video can be played inside the app (the bundled ffmpeg does the decoding).
/// </summary>
public interface IVideoPlaybackService
{
    /// <summary>
    /// Ensures ffmpeg is available for in-app playback, installing it if needed. Returns false when
    /// in-app playback isn't possible, in which case callers should open the file with the system player.
    /// </summary>
    Task<bool> EnsureAvailableAsync();
}
