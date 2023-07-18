using Microsoft.Extensions.Caching.Memory;
using Octokit;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Helper.Cache;

public class GithubApiCache : IGithubApiCache
{
    private readonly ILiteDbContext dbContext;
    private readonly IGitHubClient githubApi;
    private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(15);

    public GithubApiCache(ILiteDbContext dbContext, IGitHubClient githubApi)
    {
        this.dbContext = dbContext;
        this.githubApi = githubApi;
    }

    public async Task<Release?> GetLatestRelease(string username, string repository)
    {
        var cacheKey = $"Releases-{username}-{repository}";
        var latestRelease = await dbContext.GetGithubCacheEntry(cacheKey);
        if (latestRelease != null && !IsCacheExpired(latestRelease.LastUpdated))
        {
            return latestRelease.AllReleases.First();
        }

        var allReleases = await githubApi.Repository.Release.GetAll(username, repository);
        if (allReleases == null)
        {
            return null;
        }
        
        var cacheEntry = new GithubCacheEntry
        {
            CacheKey = cacheKey,
            AllReleases = allReleases.OrderByDescending(x => x.CreatedAt)
        };
        await dbContext.UpsertGithubCacheEntry(cacheEntry);

        return cacheEntry.AllReleases.First();
    }

    public async Task<IEnumerable<Release>> GetAllReleases(string username, string repository)
    {
        var cacheKey = $"Releases-{username}-{repository}";
        var cacheEntry = await dbContext.GetGithubCacheEntry(cacheKey);
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.AllReleases.OrderByDescending(x => x.CreatedAt);
        }

        var allReleases = await githubApi.Repository.Release.GetAll(username, repository);
        if (allReleases == null)
        {
            return new List<Release>().OrderByDescending(x => x.CreatedAt);
        }
        
        var newCacheEntry = new GithubCacheEntry
        {
            CacheKey = cacheKey,
            AllReleases = allReleases.OrderByDescending(x => x.CreatedAt)
        };
        await dbContext.UpsertGithubCacheEntry(newCacheEntry);

        return newCacheEntry.AllReleases;
    }

    public async Task<IEnumerable<Branch>> GetAllBranches(string username, string repository)
    {
        var cacheKey = $"Branches-{username}-{repository}";
        var cacheEntry = await dbContext.GetGithubCacheEntry(cacheKey);
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.Branches;
        }

        var branches = await githubApi.Repository.Branch.GetAll(username, repository);
        if (branches == null)
        {
            return new List<Branch>();
        }
        
        var newCacheEntry = new GithubCacheEntry
        {
            CacheKey = cacheKey,
            Branches = branches
        };
        await dbContext.UpsertGithubCacheEntry(newCacheEntry);

        return newCacheEntry.Branches;
    }

    public async Task<IEnumerable<GitCommit>?> GetAllCommits(string username, string repository, string branch, int page = 1, int perPage = 10)
    {
        var cacheKey = $"Commits-{username}-{repository}-{branch}-{page}-{perPage}";
        var cacheEntry = await dbContext.GetGithubCacheEntry(cacheKey);
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.Commits;
        }

        var commits = await githubApi.Repository.Commit.GetAll(username, repository, new CommitRequest {Sha = branch}, new ApiOptions
        {
            PageCount = page,
            PageSize = perPage,
            StartPage = page
        });
        
        if (commits == null)
        {
            return new List<GitCommit>();
        }
        
        var newCacheEntry = new GithubCacheEntry
        {
            CacheKey = cacheKey,
            Commits = commits.Select(x => new GitCommit { Sha = x.Sha })
        };
        await dbContext.UpsertGithubCacheEntry(newCacheEntry);

        return newCacheEntry.Commits;
    }
    
    private bool IsCacheExpired(DateTimeOffset expiration) => expiration.Add(cacheDuration) < DateTimeOffset.UtcNow;
}
