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
        PackageHelper.GetPackages().Where(x => x is not ComfyZluda).Select(p => new object[] { p });

    private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = Policy<HttpResponseMessage>
        .HandleResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(200), 3),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                // Log retry attempt if needed
                Console.WriteLine($"Retry attempt {retryAttempt}, waiting {timespan.TotalSeconds} seconds");
            }
        );

    [TestMethod]
    [DynamicData(nameof(PackagesData))]
    public async Task TestPreviewImageUri(BasePackage package)
    {
        var imageUri = package.PreviewImageUri;

        // If is GitHub Uri, use jsdelivr instead due to rate limiting
        imageUri = GitHubToJsDelivr(imageUri);

        // Test http head is successful with retry policy
        var response = await RetryPolicy.ExecuteAsync(async () =>
            await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, imageUri))
        );

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
            Assert.Inconclusive($"No LicenseUrl for package {package.GetType().Name} '{package.Name}'");
        }

        var licenseUri = new Uri(package.LicenseUrl);

        // If is GitHub Uri, use jsdelivr instead due to rate limiting
        licenseUri = GitHubToJsDelivr(licenseUri);

        // Test http head is successful with retry policy
        var response = await RetryPolicy.ExecuteAsync(async () =>
            await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, licenseUri))
        );

        Assert.IsTrue(
            response.IsSuccessStatusCode,
            "Failed to get LicenseUrl at {0}: {1}",
            licenseUri,
            response
        );
    }

    private static Uri GitHubToJsDelivr(Uri uri)
    {
        // Like https://github.com/user/Repo/blob/main/LICENSE
        // becomes: https://cdn.jsdelivr.net/gh/user/Repo@main/LICENSE
        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments is [var user, var repo, "blob", var branch, ..])
            {
                var path = string.Join("/", segments.Skip(4));
                return new Uri($"https://cdn.jsdelivr.net/gh/{user}/{repo}@{branch}/{path}");
            }
        }

        return uri;
    }
}
