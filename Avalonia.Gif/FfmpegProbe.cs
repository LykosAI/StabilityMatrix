using System.Globalization;
using System.Text.RegularExpressions;

namespace Avalonia.Gif;

/// <summary>Dimensions and frame rate of a video's first video stream.</summary>
public readonly record struct VideoStreamInfo(int Width, int Height, double Fps);

/// <summary>
/// Reads stream metadata out of ffmpeg's own stderr banner (<c>ffmpeg -i file</c>), so playback needs
/// only the ffmpeg binary and not ffprobe, which isn't bundled on every platform.
/// </summary>
public static partial class FfmpegProbe
{
    /// <summary>Used when the banner doesn't state a usable frame rate.</summary>
    public const double FallbackFps = 24.0;

    /// <summary>
    /// Parses the <c>Stream ... Video:</c> line of an ffmpeg stderr banner.
    /// </summary>
    /// <exception cref="InvalidDataException">No video stream with a readable resolution was found.</exception>
    public static VideoStreamInfo Parse(string ffmpegStdErr)
    {
        var videoLine = ffmpegStdErr
            .Split('\n')
            .FirstOrDefault(line => line.Contains("Video:", StringComparison.Ordinal))
            ?? throw new InvalidDataException("ffmpeg reported no video stream.");

        var sizeMatch = ResolutionRegex().Match(videoLine);
        if (
            !sizeMatch.Success
            || !int.TryParse(sizeMatch.Groups[1].Value, out var width)
            || !int.TryParse(sizeMatch.Groups[2].Value, out var height)
            || width <= 0
            || height <= 0
        )
        {
            throw new InvalidDataException("Could not read the video resolution from ffmpeg's output.");
        }

        var fps = FallbackFps;
        var fpsMatch = FpsRegex().Match(videoLine);
        if (
            fpsMatch.Success
            && double.TryParse(
                fpsMatch.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsedFps
            )
            && parsedFps > 0
        )
        {
            fps = parsedFps;
        }

        return new VideoStreamInfo(width, height, fps);
    }

    // First "WxH" token on the line, e.g. "1344x768 [SAR 1:1 DAR 7:4]". Word boundaries keep codec tags
    // like "0x31637661" from matching.
    [GeneratedRegex(@"\b(\d{2,5})x(\d{2,5})\b")]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*fps\b", RegexOptions.IgnoreCase)]
    private static partial Regex FpsRegex();
}
