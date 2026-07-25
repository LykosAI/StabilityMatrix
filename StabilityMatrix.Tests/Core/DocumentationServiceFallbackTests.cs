using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

/// <summary>
/// Covers the offline chain: with the network failing and no cache, both the listing and page
/// content must still come back from the bundled snapshot rather than surfacing an error.
/// </summary>
[TestClass]
public class DocumentationServiceFallbackTests
{
    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("offline");
    }

    private static DocumentationService CreateRateLimitedService()
    {
        var gitHub = Substitute.For<IGitHubClient>();
        gitHub
            .Git.Tree.GetRecursive(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Throws(new ApiException("API rate limit exceeded", HttpStatusCode.Forbidden));

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new FailingHandler()));

        return new DocumentationService(NullLogger<DocumentationService>.Instance, gitHub, factory);
    }

    [TestMethod]
    public async Task GetSectionsAsync_FallsBackToBundle_WhenApiRateLimited()
    {
        var service = CreateRateLimitedService();

        var sections = await service.GetSectionsAsync(forceRefresh: true);

        Assert.IsTrue(sections.Count > 0, "Expected the bundled listing to populate the nav tree.");
        Assert.IsTrue(
            sections.SelectMany(s => s.Pages).Any(p => p.Path == "README.md"),
            "Bundled listing should include the docs landing page."
        );
    }

    [TestMethod]
    public async Task GetPageMarkdownAsync_FallsBackToBundle_WhenOffline()
    {
        var service = CreateRateLimitedService();

        var markdown = await service.GetPageMarkdownAsync("README.md", forceRefresh: true);

        Assert.IsFalse(string.IsNullOrWhiteSpace(markdown));
    }

    [TestMethod]
    public async Task GetPageMarkdownAsync_StillThrows_ForPageNotInBundle()
    {
        var service = CreateRateLimitedService();

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            service.GetPageMarkdownAsync("nope/not-a-real-page.md", forceRefresh: true)
        );
    }
}
