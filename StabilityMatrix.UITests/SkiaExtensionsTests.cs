using SkiaSharp;
using StabilityMatrix.Avalonia.Extensions;

namespace StabilityMatrix.UITests;

public class SkiaExtensionsTests
{
    [AvaloniaFact]
    public void ToAvaloniaBitmap_OwnsPixelsAfterSourceIsDisposed()
    {
        using var source = new SKBitmap(2, 2, SKColorType.Bgra8888, SKAlphaType.Premul);
        source.SetPixel(0, 0, SKColors.Red);

        using var converted = source.ToAvaloniaBitmap();
        source.Dispose();

        using var stream = new MemoryStream();
        converted.Save(stream);
        stream.Position = 0;

        using var decoded = SKBitmap.Decode(stream);
        Assert.Equal(SKColors.Red, decoded.GetPixel(0, 0));
    }

    [AvaloniaFact]
    public void ToAvaloniaBitmap_PreservesTransparentMaskPixels()
    {
        using var source = new SKBitmap(2, 1, SKColorType.Bgra8888, SKAlphaType.Premul);
        source.SetPixel(0, 0, SKColors.Transparent);
        source.SetPixel(1, 0, new SKColor(255, 255, 255, 128));

        using var converted = source.ToAvaloniaBitmap();
        using var stream = new MemoryStream();
        converted.Save(stream);
        stream.Position = 0;

        using var decoded = SKBitmap.Decode(stream);
        Assert.Equal((byte)0, decoded.GetPixel(0, 0).Alpha);
        Assert.Equal((byte)128, decoded.GetPixel(1, 0).Alpha);
        Assert.Equal(SKColors.White.WithAlpha(128), decoded.GetPixel(1, 0));
    }
}
