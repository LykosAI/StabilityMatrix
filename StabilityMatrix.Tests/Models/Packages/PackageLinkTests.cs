using System.Net.Http.Headers;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Tests.Models.Packages;

/// <summary>
/// Tests that URL links on Packages should be valid. Requires internet connection.
/// </summary>
[TestClass]
public sealed class PackageLinkTests
{
    private static HttpClient HttpClient { get; } =
        new() { DefaultRequestHeaders = { { "User-Agent", "StabilityMatrix/2.0" } } };

    private static IEnumerable<object[]> PackagesData =>
        PackageHelper.GetPackages().Select(p => new object[] { p });

    [TestMethod]
    [DynamicData(nameof(PackagesData))]
    public async Task TestPreviewImageUri(BasePackage package)
    {
        var imageUri = package.PreviewImageUri;

        // Test http head is successful
        var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, imageUri));

        Assert.IsTrue(
            response.IsSuccessStatusCode,
            "Failed to get PreviewImageUri at {0}: {1}",
            imageUri,
            response
        );
    }

    [TestMethod]
    [DynamicData(nameof(PackagesData))]
    public async Task TestLicenseUrl(BasePackage package)
    {
        if (string.IsNullOrEmpty(package.LicenseUrl))
        {
            Assert.Inconclusive();
        }

        var licenseUri = package.LicenseUrl;

        // Test http head is successful
        var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, licenseUri));

        Assert.IsTrue(
            response.IsSuccessStatusCode,
            "Failed to get LicenseUrl at {0}: {1}",
            licenseUri,
            response
        );
    }
}
