using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Decides whether a video can be played inside the app (the bundled ffmpeg does the decoding) and
/// provides the external-player fallback used when it can't.
/// </summary>
public interface IVideoPlaybackService
{
    /// <summary>
    /// Ensures ffmpeg is available for in-app playback, installing it if needed. Returns false when
    /// in-app playback isn't possible, in which case callers should fall back to <see cref="OpenExternally"/>.
    /// </summary>
    Task<bool> EnsureAvailableAsync();

    /// <summary>
    /// Opens the video in the system's default player. In-app playback is silent, so this is also how a
    /// user hears a clip's audio track.
    /// </summary>
    void OpenExternally(string videoPath);
}
