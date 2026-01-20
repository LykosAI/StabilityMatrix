using System.Net.Http.Headers;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Tests.Models.Packages;

/// <summary>
/// Tests that URL links on Packages should be valid. Requires internet connection.
/// </summary>
[TestClass]
[TestCategory("Http")]
public sealed class PackageLinkTests
{
    private static HttpClient HttpClient { get; } =
        new() { DefaultRequestHeaders = { { "User-Agent", "StabilityMatrix/2.0" } } };

    private static IEnumerable<object[]> PackagesData =>
        PackageHelper.GetPackages().Select(p => new object[] { p });

    private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = Policy<HttpResponseMessage>
        .HandleResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(200), 3),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"Retry attempt {retryAttempt}, waiting {timespan.TotalSeconds} seconds");
            }
        );

    [TestMethod]
    [DynamicData(nameof(PackagesData))]
    public async Task TestPreviewImageUri(BasePackage package)
    {
        var imageUri = package.PreviewImageUri;

        // If GitHub URL, use jsDelivr to avoid rate limiting
        imageUri = GitHubToJsDelivr(imageUri);

        var response = await RetryPolicy.ExecuteAsync(async () =>
            await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, imageUri))
        );

        // 403 should fail — URL is invalid or blocked
        Assert.AreNotEqual(
            System.Net.HttpStatusCode.Forbidden,
            response.StatusCode,
            $"PreviewImageUri returned 403 Forbidden: {imageUri}"
        );

        Assert.IsTrue(
            response.IsSuccessStatusCode,
            $"Failed to get PreviewImageUri at {imageUri}: {response}"
        );
    }

    [TestMethod]
    [DynamicData(nameof(PackagesData))]
    public async Task TestLicenseUrl(BasePackage package)
    {
        if (string.IsNullOrEmpty(package.LicenseUrl))
        {
            Assert.Inconclusive($"No LicenseUrl for package {package.GetType().Name} '{package.Name}'");
        }

        var licenseUri = new Uri(package.LicenseUrl);

        // If GitHub URL, use jsDelivr to avoid rate limiting
        licenseUri = GitHubToJsDelivr(licenseUri);

        var response = await RetryPolicy.ExecuteAsync(async () =>
            await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, licenseUri))
        );

        Assert.IsTrue(response.IsSuccessStatusCode, $"Failed to get LicenseUrl at {licenseUri}: {response}");
    }

    private static Uri GitHubToJsDelivr(Uri uri)
    {
        // Example:
        // https://github.com/user/Repo/blob/main/LICENSE
        // becomes:
        // https://cdn.jsdelivr.net/gh/user/Repo@main/LICENSE

        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return uri;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments is [var user, var repo, "blob", var branch, ..])
        {
            var path = string.Join("/", segments.Skip(4));
            return new Uri($"https://cdn.jsdelivr.net/gh/{user}/{repo}@{branch}/{path}");
        }

        return uri;
    }
}
