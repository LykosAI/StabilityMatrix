using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Helper.Cache;

[RegisterSingleton<IGithubApiCache, GithubApiCache>]
public class GithubApiCache(
    ILiteDbContext dbContext,
    IGitHubClient githubApi,
    ILogger<IGithubApiCache> logger
) : IGithubApiCache
{
    private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(15);

    public async Task<IEnumerable<Release>> GetAllReleases(string username, string repository)
    {
        var cacheKey = $"Releases-{username}-{repository}";
        var cacheEntry = await dbContext.GetGithubCacheEntry(cacheKey).ConfigureAwait(false);
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.AllReleases.OrderByDescending(x => x.CreatedAt);
        }

        try
        {
            var allReleases = await githubApi
                .Repository.Release.GetAll(username, repository)
                .ConfigureAwait(false);
            if (allReleases == null)
            {
                return new List<Release>();
            }

            var newCacheEntry = new GithubCacheEntry
            {
                CacheKey = cacheKey,
                AllReleases = allReleases.OrderByDescending(x => x.CreatedAt)
            };
            await dbContext.UpsertGithubCacheEntry(newCacheEntry).ConfigureAwait(false);

            return newCacheEntry.AllReleases;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get releases from Github API.");
            return cacheEntry?.AllReleases.OrderByDescending(x => x.CreatedAt) ?? Enumerable.Empty<Release>();
        }
    }

    public async Task<IEnumerable<Branch>> GetAllBranches(string username, string repository)
    {
        var cacheKey = $"Branches-{username}-{repository}";
        var cacheEntry = await dbContext.GetGithubCacheEntry(cacheKey).ConfigureAwait(false);
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.Branches;
        }

        try
        {
            var branches = await githubApi
                .Repository.Branch.GetAll(username, repository)
                .ConfigureAwait(false);
            if (branches == null)
            {
                return new List<Branch>();
            }

            var newCacheEntry = new GithubCacheEntry { CacheKey = cacheKey, Branches = branches };
            await dbContext.UpsertGithubCacheEntry(newCacheEntry).ConfigureAwait(false);

            return newCacheEntry.Branches;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get branches from Github API.");
            return cacheEntry?.Branches ?? [];
        }
    }

    public async Task<IEnumerable<GitCommit>?> GetAllCommits(
        string username,
        string repository,
        string branch,
        int page = 1,
        int perPage = 10
    )
    {
        var cacheKey = $"Commits-{username}-{repository}-{branch}-{page}-{perPage}";
        var cacheEntry = await dbContext.GetGithubCacheEntry(cacheKey).ConfigureAwait(false);
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.Commits;
        }

        try
        {
            var commits = await githubApi
                .Repository.Commit.GetAll(
                    username,
                    repository,
                    new CommitRequest { Sha = branch },
                    new ApiOptions
                    {
                        PageCount = page,
                        PageSize = perPage,
                        StartPage = page
                    }
                )
                .ConfigureAwait(false);

            if (commits == null)
            {
                return new List<GitCommit>();
            }

            var newCacheEntry = new GithubCacheEntry
            {
                CacheKey = cacheKey,
                Commits = commits.Select(x => new GitCommit { Sha = x.Sha })
            };
            await dbContext.UpsertGithubCacheEntry(newCacheEntry).ConfigureAwait(false);

            return newCacheEntry.Commits;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get commits from Github API.");
            return cacheEntry?.Commits ?? [];
        }
    }

    private bool IsCacheExpired(DateTimeOffset expiration) =>
        expiration.Add(cacheDuration) < DateTimeOffset.UtcNow;
}
