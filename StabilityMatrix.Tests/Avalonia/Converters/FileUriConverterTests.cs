using System.Globalization;
using StabilityMatrix.Avalonia.Converters;

namespace StabilityMatrix.Tests.Avalonia.Converters;

[TestClass]
public class FileUriConverterTests
{
    private readonly FileUriConverter converter = new();

    [TestMethod]
    public void Convert_PathWithHashAndSpaces_LocalPathRoundTrips()
    {
        // '#' from user file name patterns must not be parsed as a Uri fragment
        var path = Path.Combine(Path.GetTempPath(), "cool #model v2.preview.png");

        var result = converter.Convert(path, typeof(Uri), null, CultureInfo.InvariantCulture);

        var uri = result as Uri;
        Assert.IsNotNull(uri);
        Assert.AreEqual("file", uri.Scheme);
        Assert.AreEqual(path, uri.LocalPath);
    }

    [TestMethod]
    public void Convert_PathWithPercent_LocalPathRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "model 50%25.preview.png");

        var result = converter.Convert(path, typeof(Uri), null, CultureInfo.InvariantCulture);

        var uri = result as Uri;
        Assert.IsNotNull(uri);
        Assert.AreEqual(path, uri.LocalPath);
    }

    [TestMethod]
    public void Convert_HttpUrl_PassesThrough()
    {
        const string url = "https://example.org/images/1.png";

        var result = converter.Convert(url, typeof(Uri), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(new Uri(url), result);
    }

    [TestMethod]
    public void Convert_AvaresUrl_PassesThrough()
    {
        const string url = "avares://StabilityMatrix.Avalonia/Assets/noimage.png";

        var result = converter.Convert(url, typeof(Uri), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(new Uri(url), result);
    }

    [TestMethod]
    public void Convert_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(converter.Convert(null, typeof(Uri), null, CultureInfo.InvariantCulture));
        Assert.IsNull(converter.Convert("", typeof(Uri), null, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ConvertBack_FileUri_ReturnsLocalPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "cool #model v2.preview.png");
        var uri = new Uri(path);

        var result = converter.ConvertBack(uri, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(path, result);
    }
}
