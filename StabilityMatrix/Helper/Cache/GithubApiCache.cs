using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Octokit;
using StabilityMatrix.Api;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Helper.Cache;

public class GithubApiCache : IGithubApiCache
{
    private readonly IMemoryCache memoryCache;
    private readonly IGitHubClient githubApi;
    private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(5);

    public GithubApiCache(IMemoryCache memoryCache, IGitHubClient githubApi)
    {
        this.memoryCache = memoryCache;
        this.githubApi = githubApi;
    }

    public Task<Release> GetLatestRelease(string username, string repository)
    {
        var cacheKey = $"LatestRelease-{username}-{repository}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            return await githubApi.Repository.Release.GetLatest(username, repository);
        })!;
    }

    public Task<IOrderedEnumerable<Release>> GetAllReleases(string username, string repository)
    {
        var cacheKey = $"Releases-{username}-{repository}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            var allReleases = await githubApi.Repository.Release.GetAll(username, repository);

            return allReleases.OrderByDescending(x => x.CreatedAt);
        })!;
    }

    public Task<IReadOnlyList<Branch>> GetAllBranches(string username, string repository)
    {
        var cacheKey = $"Branches-{username}-{repository}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            var allReleases = await githubApi.Repository.Branch.GetAll(username, repository);
            return allReleases;
        })!;
    }

    public Task<IReadOnlyList<GitHubCommit>?> GetAllCommits(string username, string repository, string branch, int page = 1, int perPage = 10)
    {
        var cacheKey = $"Commits-{username}-{repository}-{branch}-{page}-{perPage}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            return await githubApi.Repository.Commit.GetAll(username, repository, new CommitRequest {Sha = branch},
                new ApiOptions
                {
                    PageCount = page,
                    PageSize = perPage,
                    StartPage = page
                });
        })!;
    }
}
