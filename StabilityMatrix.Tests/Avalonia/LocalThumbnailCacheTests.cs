using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class LocalThumbnailCacheTests
{
    private string root = null!;
    private string thumbnails = null!;
    private LocalThumbnailCache cache = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), $"sm-thumbs-{Guid.NewGuid():N}");
        thumbnails = Path.Combine(root, "thumbs");
        Directory.CreateDirectory(root);
        cache = new LocalThumbnailCache(thumbnails);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public void Decode_WideImage_ScalesAndStoresOneThumbnail()
    {
        var source = WritePng("wide.png", 1200, 800, SKColors.Blue);

        using var first = cache.Decode(source, 300);
        using var second = cache.Decode(source, 300);

        Assert.AreEqual((300, 200), (first!.Width, first.Height));
        Assert.AreEqual((300, 200), (second!.Width, second.Height));
        Assert.AreEqual(1, Directory.GetFiles(thumbnails, "*.webp").Length);
    }

    [TestMethod]
    public void Decode_SourceReplaced_ReturnsNewImage()
    {
        var source = WritePng("model.preview.png", 1200, 800, SKColors.Blue);
        using (var before = cache.Decode(source, 300))
        {
            AssertMostly(before!, SKColors.Blue);
        }

        File.Delete(source);
        WritePng("model.preview.png", 1000, 1000, SKColors.Red);

        using var after = cache.Decode(source, 300);

        Assert.AreEqual((300, 300), (after!.Width, after.Height));
        AssertMostly(after, SKColors.Red);
    }

    [TestMethod]
    public void Decode_ImageNoWiderThanTarget_IsNotStored()
    {
        var source = WritePng("small.png", 200, 300, SKColors.Green);

        using var bitmap = cache.Decode(source, 300);

        Assert.AreEqual((200, 300), (bitmap!.Width, bitmap.Height));
        Assert.IsFalse(Directory.Exists(thumbnails) && Directory.EnumerateFiles(thumbnails).Any());
    }

    [TestMethod]
    public void Decode_CorruptThumbnail_IsRebuilt()
    {
        var source = WritePng("wide.png", 1200, 800, SKColors.Blue);
        cache.Decode(source, 300)!.Dispose();
        var thumbnail = Directory.GetFiles(thumbnails, "*.webp").Single();
        File.WriteAllBytes(thumbnail, [1, 2, 3]);

        using var bitmap = cache.Decode(source, 300);

        Assert.AreEqual((300, 200), (bitmap!.Width, bitmap.Height));
        AssertMostly(bitmap, SKColors.Blue);
        Assert.IsNotNull(SKBitmap.Decode(thumbnail), "rebuilt thumbnail should be a valid image");
    }

    private string WritePng(string name, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static void AssertMostly(SKBitmap bitmap, SKColor expected)
    {
        // Thumbnails are lossy WebP, so compare loosely.
        var actual = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.IsTrue(
            Math.Abs(actual.Red - expected.Red) < 16
                && Math.Abs(actual.Green - expected.Green) < 16
                && Math.Abs(actual.Blue - expected.Blue) < 16,
            $"Expected about {expected}, got {actual}"
        );
    }
}
