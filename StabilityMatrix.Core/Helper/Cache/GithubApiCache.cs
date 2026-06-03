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

    /// <summary>
    /// Tracks when we're rate limited until. If set and in the future, skip API calls.
    /// </summary>
    private DateTimeOffset? rateLimitedUntil;

    /// <summary>
    /// Lock for thread-safe rate limit state updates
    /// </summary>
    private readonly object rateLimitLock = new();

    /// <summary>
    /// Checks if we're currently rate limited and should skip API calls
    /// </summary>
    private bool IsRateLimited
    {
        get
        {
            lock (rateLimitLock)
            {
                if (rateLimitedUntil == null)
                    return false;

                if (DateTimeOffset.UtcNow >= rateLimitedUntil)
                {
                    // Rate limit has expired, clear it
                    rateLimitedUntil = null;
                    logger.LogInformation("GitHub API rate limit period has expired, resuming API calls");
                    return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Sets the rate limited state based on the exception
    /// </summary>
    private void SetRateLimited(RateLimitExceededException ex)
    {
        lock (rateLimitLock)
        {
            // Use the reset time from the exception if available, otherwise default to 5 minutes
            var resetTime =
                ex.Reset != DateTimeOffset.MinValue ? ex.Reset : DateTimeOffset.UtcNow.AddMinutes(5);

            // Only update if this extends our rate limit period
            if (rateLimitedUntil == null || resetTime > rateLimitedUntil)
            {
                rateLimitedUntil = resetTime;
                logger.LogWarning(
                    "GitHub API rate limit exceeded. Skipping API calls until {ResetTime} ({TimeRemaining} remaining)",
                    resetTime.LocalDateTime,
                    resetTime - DateTimeOffset.UtcNow
                );
            }
        }
    }

    public async Task<IEnumerable<Release>> GetAllReleases(string username, string repository)
    {
        var cacheKey = $"Releases-{username}-{repository}";
        var cacheEntry = await dbContext.GetGithubCacheEntry(cacheKey).ConfigureAwait(false);

        // Return cached data if not expired
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.AllReleases.OrderByDescending(x => x.CreatedAt);
        }

        // If rate limited, return cached data without making API call
        if (IsRateLimited)
        {
            logger.LogDebug(
                "Skipping GitHub API call for {Username}/{Repository} releases due to rate limiting",
                username,
                repository
            );
            return cacheEntry?.AllReleases.OrderByDescending(x => x.CreatedAt) ?? Enumerable.Empty<Release>();
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
                AllReleases = allReleases.OrderByDescending(x => x.CreatedAt),
            };
            await dbContext.UpsertGithubCacheEntry(newCacheEntry).ConfigureAwait(false);

            return newCacheEntry.AllReleases;
        }
        catch (RateLimitExceededException ex)
        {
            SetRateLimited(ex);
            return cacheEntry?.AllReleases.OrderByDescending(x => x.CreatedAt) ?? Enumerable.Empty<Release>();
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

        // Return cached data if not expired
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.Branches;
        }

        // If rate limited, return cached data without making API call
        if (IsRateLimited)
        {
            logger.LogDebug(
                "Skipping GitHub API call for {Username}/{Repository} branches due to rate limiting",
                username,
                repository
            );
            return cacheEntry?.Branches ?? [];
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
        catch (RateLimitExceededException ex)
        {
            SetRateLimited(ex);
            return cacheEntry?.Branches ?? [];
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

        // Return cached data if not expired
        if (cacheEntry != null && !IsCacheExpired(cacheEntry.LastUpdated))
        {
            return cacheEntry.Commits;
        }

        // If rate limited, return cached data without making API call
        if (IsRateLimited)
        {
            logger.LogDebug(
                "Skipping GitHub API call for {Username}/{Repository} commits due to rate limiting",
                username,
                repository
            );
            return cacheEntry?.Commits ?? [];
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
                        StartPage = page,
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
                Commits = commits.Select(x => new GitCommit { Sha = x.Sha }),
            };
            await dbContext.UpsertGithubCacheEntry(newCacheEntry).ConfigureAwait(false);

            return newCacheEntry.Commits;
        }
        catch (RateLimitExceededException ex)
        {
            SetRateLimited(ex);
            return cacheEntry?.Commits ?? [];
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
