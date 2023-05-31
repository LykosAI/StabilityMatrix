using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using StabilityMatrix.Api;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Helper.Cache;

public class GithubApiCache : IGithubApiCache
{
    private readonly IMemoryCache memoryCache;
    private readonly IGithubApi githubApi;
    private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(5);

    public GithubApiCache(IMemoryCache memoryCache, IGithubApi githubApi)
    {
        this.memoryCache = memoryCache;
        this.githubApi = githubApi;
    }

    public Task<GithubRelease> GetLatestRelease(string username, string repository)
    {
        var cacheKey = $"LatestRelease-{username}-{repository}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            return await githubApi.GetLatestRelease(username, repository);
        })!;
    }

    public Task<IEnumerable<GithubRelease>> GetAllReleases(string username, string repository)
    {
        var cacheKey = $"Releases-{username}-{repository}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            return await githubApi.GetAllReleases(username, repository);
        })!;
    }

    public Task<IEnumerable<GithubBranch>> GetAllBranches(string username, string repository)
    {
        var cacheKey = $"Branches-{username}-{repository}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            return await githubApi.GetAllBranches(username, repository);
        })!;
    }

    public Task<IEnumerable<GithubCommit>> GetAllCommits(string username, string repository, string branch, int page = 1, int perPage = 10)
    {
        var cacheKey = $"Commits-{username}-{repository}-{branch}-{page}-{perPage}";
        return memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration;
            return await githubApi.GetAllCommits(username, repository, branch, page, perPage);
        })!;
    }
}
