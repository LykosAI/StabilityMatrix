using Microsoft.Extensions.Caching.Memory;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Git;

public class CachedCommandGitVersionProvider(string repositoryUri, IPrerequisiteHelper prerequisiteHelper)
    : IGitVersionProvider
{
    private readonly CommandGitVersionProvider commandGitVersionProvider =
        new(repositoryUri, prerequisiteHelper);
    private readonly IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());

    public async Task<IReadOnlyList<GitVersion>> FetchTagsAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await memoryCache
                .GetOrCreateAsync(
                    "tags",
                    async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        return await commandGitVersionProvider
                            .FetchTagsAsync(limit, cancellationToken)
                            .ConfigureAwait(false);
                    }
                )
                .ConfigureAwait(false)
        )!;
    }

    public async Task<IReadOnlyList<GitVersion>> FetchBranchesAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await memoryCache
                .GetOrCreateAsync(
                    "branches",
                    async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        return await commandGitVersionProvider
                            .FetchBranchesAsync(limit, cancellationToken)
                            .ConfigureAwait(false);
                    }
                )
                .ConfigureAwait(false)
        )!;
    }

    public async Task<IReadOnlyList<GitVersion>> FetchCommitsAsync(
        string? branch,
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await memoryCache
                .GetOrCreateAsync(
                    $"commits-{branch}",
                    async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        return await commandGitVersionProvider
                            .FetchCommitsAsync(branch, limit, cancellationToken)
                            .ConfigureAwait(false);
                    }
                )
                .ConfigureAwait(false)
        )!;
    }
}
