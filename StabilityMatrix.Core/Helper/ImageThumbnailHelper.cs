using SkiaSharp;

namespace StabilityMatrix.Core.Helper;

public static class ImageThumbnailHelper
{
    /// <summary>
    /// Downscales an image to fit within <paramref name="maxDimension"/> on its longest side,
    /// re-encoded as PNG. Returns the original bytes when the image is already small enough
    /// or cannot be decoded.
    /// </summary>
    public static byte[] CreateThumbnail(byte[] imageBytes, int maxDimension = 1024)
    {
        SKBitmap? bitmap;
        try
        {
            bitmap = SKBitmap.Decode(imageBytes);
        }
        catch (Exception)
        {
            return imageBytes;
        }

        if (bitmap is null)
            return imageBytes;

        using var _ = bitmap;

        var scale = (double)maxDimension / Math.Max(bitmap.Width, bitmap.Height);
        if (scale >= 1)
            return imageBytes;

        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        using var resized = bitmap.Resize(
            new SKImageInfo(width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
        );
        if (resized is null)
            return imageBytes;

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
    }
}
