using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Extensions;

public static class BitmapExtensions
{
    /// <summary>
    /// Converts an Avalonia <see cref="IBitmap"/> to a SkiaSharp <see cref="SKBitmap"/>.
    /// </summary>
    /// <param name="bitmap">The Avalonia bitmap to convert.</param>
    /// <returns>The SkiaSharp bitmap.</returns>
    public static SKBitmap ToSKBitmap(this Bitmap bitmap)
    {
        var skBitmap = new SKBitmap(
            bitmap.PixelSize.Width,
            bitmap.PixelSize.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul
        );

        var stride = skBitmap.RowBytes;
        var bufferSize = stride * skBitmap.Height;
        var sourceRect = new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        bitmap.CopyPixels(sourceRect, skBitmap.GetPixels(), bufferSize, stride);

        return skBitmap;
    }
}
