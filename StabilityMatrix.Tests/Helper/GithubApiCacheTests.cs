using Microsoft.Extensions.Logging;
using NSubstitute;
using Octokit;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class GithubApiCacheTests
{
    private readonly IGitHubClient github = Substitute.For<IGitHubClient>();
    private readonly ILiteDbContext dbContext = Substitute.For<ILiteDbContext>();
    private GithubApiCache cache = null!;

    [TestInitialize]
    public void Setup()
    {
        dbContext.GetGithubCacheEntry(Arg.Any<string>()).Returns((GithubCacheEntry?)null);
        cache = new GithubApiCache(dbContext, github, Substitute.For<ILogger<IGithubApiCache>>());
    }

    [TestMethod]
    public async Task GetAllReleases_ConcurrentCallsForSameRepo_ShareOneRequest()
    {
        var response = new TaskCompletionSource<IReadOnlyList<Release>>();
        github
            .Repository.Release.GetAll("Comfy-Org", "ComfyUI", Arg.Any<ApiOptions>())
            .Returns(response.Task);

        var calls = Enumerable.Range(0, 5).Select(_ => cache.GetAllReleases("Comfy-Org", "ComfyUI")).ToList();
        response.SetResult([new Release()]);
        var results = await Task.WhenAll(calls);

        await github.Repository.Release.Received(1).GetAll("Comfy-Org", "ComfyUI", Arg.Any<ApiOptions>());
        Assert.IsTrue(results.All(r => r.Count() == 1));
    }

    [TestMethod]
    public async Task GetAllReleases_AfterRequestCompletes_FetchesAgainOnCacheMiss()
    {
        github
            .Repository.Release.GetAll("Comfy-Org", "ComfyUI", Arg.Any<ApiOptions>())
            .Returns([new Release()]);

        await cache.GetAllReleases("Comfy-Org", "ComfyUI");
        await cache.GetAllReleases("Comfy-Org", "ComfyUI");

        await github.Repository.Release.Received(2).GetAll("Comfy-Org", "ComfyUI", Arg.Any<ApiOptions>());
    }

    [TestMethod]
    public async Task GetAllBranches_ConcurrentCallsForSameRepo_ShareOneRequest()
    {
        var response = new TaskCompletionSource<IReadOnlyList<Branch>>();
        github.Repository.Branch.GetAll("Comfy-Org", "ComfyUI").Returns(response.Task);

        var calls = Enumerable.Range(0, 5).Select(_ => cache.GetAllBranches("Comfy-Org", "ComfyUI")).ToList();
        response.SetResult([new Branch()]);
        await Task.WhenAll(calls);

        await github.Repository.Branch.Received(1).GetAll("Comfy-Org", "ComfyUI");
    }
}
