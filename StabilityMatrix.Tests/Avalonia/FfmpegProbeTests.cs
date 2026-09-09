using System.IO;
using Avalonia.Gif;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class FfmpegProbeTests
{
    // Trimmed from a real `ffmpeg -hide_banner -i` banner for a MiniMax H3 mp4 (video + stereo audio).
    private const string H3Banner = """
        Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'video_00001_.mp4':
          Metadata:
            major_brand     : isom
          Duration: 00:00:05.00, start: 0.000000, bitrate: 1231 kb/s
          Stream #0:0[0x1](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p(progressive), 1344x768 [SAR 1:1 DAR 7:4], 1100 kb/s, 24 fps, 24 tbr, 12288 tbn (default)
          Stream #0:1[0x2](und): Audio: aac (LC) (mp4a / 0x6134706D), 48000 Hz, stereo, fltp, 128 kb/s (default)
        At least one output file must be specified
        """;

    [TestMethod]
    public void Parse_ReadsResolutionAndFpsFromVideoLine()
    {
        var info = FfmpegProbe.Parse(H3Banner);

        Assert.AreEqual(new VideoStreamInfo(1344, 768, 24), info);
    }

    [TestMethod]
    public void Parse_HandlesFractionalFps()
    {
        var banner = H3Banner.Replace("24 fps", "23.98 fps");

        Assert.AreEqual(23.98, FfmpegProbe.Parse(banner).Fps, 1e-9);
    }

    [TestMethod]
    public void Parse_FallsBackToDefaultFpsWhenMissing()
    {
        var banner = H3Banner.Replace(", 24 fps", "");

        Assert.AreEqual(FfmpegProbe.FallbackFps, FfmpegProbe.Parse(banner).Fps);
    }

    [TestMethod]
    public void Parse_IgnoresAudioLineNumbers()
    {
        // "48000 Hz" and "128 kb/s" on the audio line must never be read as a frame rate or size.
        var videoOnlyAtEnd = """
            Stream #0:1[0x2](und): Audio: aac (LC) (mp4a / 0x6134706D), 48000 Hz, stereo, fltp, 128 kb/s
            Stream #0:0[0x1](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p, 512x288, 30 fps
            """;

        Assert.AreEqual(new VideoStreamInfo(512, 288, 30), FfmpegProbe.Parse(videoOnlyAtEnd));
    }

    [TestMethod]
    public void Parse_ThrowsWithoutVideoStream()
    {
        const string audioOnly = """
            Stream #0:0(und): Audio: aac (LC) (mp4a / 0x6134706D), 48000 Hz, stereo, fltp, 128 kb/s
            """;

        Assert.ThrowsException<InvalidDataException>(() => FfmpegProbe.Parse(audioOnly));
    }
}
