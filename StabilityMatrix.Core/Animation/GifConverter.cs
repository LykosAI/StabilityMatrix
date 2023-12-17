using KGySoft.Drawing.Imaging;
using KGySoft.Drawing.SkiaSharp;
using SkiaSharp;

namespace StabilityMatrix.Core.Animation;

public class GifConverter
{
    public static IEnumerable<IReadableBitmapData> EnumerateAnimatedWebp(Stream webpSource)
    {
        using var webp = new SKManagedStream(webpSource);
        using var codec = SKCodec.Create(webp);

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);

        for (var i = 0; i < codec.FrameCount; i++)
        {
            using var tempSurface = new SKBitmap(info);

            codec.GetFrameInfo(i, out var frameInfo);

            var decodeInfo = info.WithAlphaType(frameInfo.AlphaType);

            tempSurface.TryAllocPixels(decodeInfo);

            var result = codec.GetPixels(decodeInfo, tempSurface.GetPixels(), new SKCodecOptions(i));

            if (result != SKCodecResult.Success)
                throw new InvalidDataException($"Could not decode frame {i} of {codec.FrameCount}.");

            using var peekPixels = tempSurface.PeekPixels();

            yield return peekPixels.GetReadableBitmapData(WorkingColorSpace.Default);
        }
    }

    public static Task ConvertAnimatedWebpToGifAsync(Stream webpSource, Stream gifOutput)
    {
        var gifBitmaps = EnumerateAnimatedWebp(webpSource);

        return GifEncoder.EncodeAnimationAsync(
            new AnimatedGifConfiguration(gifBitmaps, TimeSpan.FromMilliseconds(150))
            {
                Quantizer = OptimizedPaletteQuantizer.Wu(alphaThreshold: 0),
                AllowDeltaFrames = true
            },
            gifOutput
        );
    }
}
