using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Git;

public interface IGitVersionProvider
{
    /// <summary>
    /// Fetches all tags from the remote repository.
    /// </summary>
    Task<IReadOnlyList<GitVersion>> FetchTagsAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Fetches all branches from the remote repository.
    /// </summary>
    Task<IReadOnlyList<GitVersion>> FetchBranchesAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Fetch the latest commits for a branch.
    /// If null, the default branch is used.
    /// </summary>
    Task<IReadOnlyList<GitVersion>> FetchCommitsAsync(
        string? branch = null,
        int limit = 0,
        CancellationToken cancellationToken = default
    );
}
