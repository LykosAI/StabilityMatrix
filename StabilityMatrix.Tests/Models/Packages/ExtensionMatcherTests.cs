using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class ExtensionMatcherTests
{
    private const string GgufUrl = "https://github.com/city96/ComfyUI-GGUF";

    [DataTestMethod]
    [DataRow("https://github.com/city96/ComfyUI-GGUF", "github.com/city96/ComfyUI-GGUF")]
    [DataRow("https://github.com/city96/ComfyUI-GGUF.git", "github.com/city96/ComfyUI-GGUF")]
    [DataRow("https://github.com/city96/ComfyUI-GGUF/", "github.com/city96/ComfyUI-GGUF")]
    [DataRow("http://www.github.com/city96/ComfyUI-GGUF", "github.com/city96/ComfyUI-GGUF")]
    [DataRow("git@github.com:city96/ComfyUI-GGUF.git", "github.com/city96/ComfyUI-GGUF")]
    [DataRow("ssh://git@github.com/city96/ComfyUI-GGUF", "github.com/city96/ComfyUI-GGUF")]
    [DataRow("https://token@github.com/city96/ComfyUI-GGUF", "github.com/city96/ComfyUI-GGUF")]
    [DataRow("", null)]
    [DataRow(null, null)]
    public void TestNormalizeRepositoryUrl(string? url, string? expected)
    {
        Assert.AreEqual(expected, ExtensionMatcher.NormalizeRepositoryUrl(url));
    }

    [DataTestMethod]
    [DataRow("https://github.com/city96/ComfyUI-GGUF.git")]
    [DataRow("git@github.com:city96/ComfyUI-GGUF.git")]
    [DataRow("https://github.com/City96/comfyui-gguf")]
    public void TestFindInstalledMatchesEquivalentRemoteUrls(string installedUrl)
    {
        var installed = CreateInstalled("SomeFolderName", installedUrl);

        Assert.IsNotNull(ExtensionMatcher.FindInstalled(ExtensionSpecifier.Parse(GgufUrl), [installed]));
    }

    /// <summary>
    /// Registry installs (e.g. ComfyUI-Manager's) unpack a zip into a folder named after the
    /// node id, with no git metadata to compare.
    /// </summary>
    [TestMethod]
    public void TestFindInstalledMatchesFolderNameWhenNoRemoteUrl()
    {
        var installed = CreateInstalled("comfyui-gguf", null);

        Assert.IsNotNull(ExtensionMatcher.FindInstalled(ExtensionSpecifier.Parse(GgufUrl), [installed]));
    }

    [TestMethod]
    public void TestFindInstalledPrefersRemoteUrlMatchOverFolderName()
    {
        var folderNameMatch = CreateInstalled("ComfyUI-GGUF", null);
        var urlMatch = CreateInstalled("ComfyUI-GGUF-fork", $"{GgufUrl}.git");

        var result = ExtensionMatcher.FindInstalled(
            ExtensionSpecifier.Parse(GgufUrl),
            [folderNameMatch, urlMatch]
        );

        Assert.AreEqual(urlMatch, result);
    }

    [TestMethod]
    public void TestGetMissingReturnsUnmatchedSpecifiers()
    {
        const string otherUrl = "https://github.com/some-author/ComfyUI-Other";

        var missing = ExtensionMatcher.GetMissing(
            [ExtensionSpecifier.Parse(GgufUrl), ExtensionSpecifier.Parse(otherUrl)],
            [CreateInstalled("comfyui-gguf", null)]
        );

        Assert.AreEqual(1, missing.Count);
        Assert.AreEqual(otherUrl, missing[0].Name);
    }

    private static InstalledPackageExtension CreateInstalled(string folderName, string? gitRepositoryUrl) =>
        new()
        {
            Paths = [new DirectoryPath("custom_nodes", folderName)],
            GitRepositoryUrl = gitRepositoryUrl,
        };
}
