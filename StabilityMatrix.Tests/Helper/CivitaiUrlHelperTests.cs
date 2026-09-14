using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class CivitaiUrlHelperTests
{
    private const string Cdn =
        "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/73328087-0980-4624-9f00-c6d7f9f88552";

    [TestMethod]
    public void CapImageWidth_ReplacesOriginalTransform()
    {
        var result = CivitaiUrlHelper.CapImageWidth($"{Cdn}/original=true/140742447.jpeg", 450);

        Assert.AreEqual($"{Cdn}/width=450/140742447.jpeg", result);
    }

    [TestMethod]
    public void CapImageWidth_ReplacesOriginalTransform_KeepingOtherOptions()
    {
        var result = CivitaiUrlHelper.CapImageWidth($"{Cdn}/original=true,quality=90/img.jpeg", 450);

        Assert.AreEqual($"{Cdn}/width=450,quality=90/img.jpeg", result);
    }

    [TestMethod]
    public void CapImageWidth_CapsWiderTransform()
    {
        var result = CivitaiUrlHelper.CapImageWidth($"{Cdn}/width=1024/img.jpeg", 450);

        Assert.AreEqual($"{Cdn}/width=450/img.jpeg", result);
    }

    [TestMethod]
    public void CapImageWidth_LeavesNarrowerTransform()
    {
        var url = $"{Cdn}/width=300/img.jpeg";

        Assert.AreEqual(url, CivitaiUrlHelper.CapImageWidth(url, 450));
    }

    [TestMethod]
    public void CapImageWidth_LeavesOtherHosts()
    {
        const string url = "https://example.com/original=true/img.png";

        Assert.AreEqual(url, CivitaiUrlHelper.CapImageWidth(url, 450));
    }
}
