using SkiaSharp;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class ImageThumbnailHelperTests
{
    [TestMethod]
    public void DownscalesLargeImages_PreservingAspectRatio()
    {
        var thumbnail = ImageThumbnailHelper.CreateThumbnail(EncodePng(4000, 2000), maxDimension: 1024);

        using var decoded = SKBitmap.Decode(thumbnail);
        Assert.AreEqual(1024, decoded.Width);
        Assert.AreEqual(512, decoded.Height);
    }

    [TestMethod]
    public void LeavesSmallImagesUnchanged()
    {
        var original = EncodePng(200, 100);

        Assert.AreSame(original, ImageThumbnailHelper.CreateThumbnail(original, maxDimension: 1024));
    }

    [TestMethod]
    public void LeavesUndecodableInputUnchanged()
    {
        var garbage = new byte[] { 1, 2, 3, 4, 5 };

        Assert.AreSame(garbage, ImageThumbnailHelper.CreateThumbnail(garbage));
    }

    private static byte[] EncodePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
