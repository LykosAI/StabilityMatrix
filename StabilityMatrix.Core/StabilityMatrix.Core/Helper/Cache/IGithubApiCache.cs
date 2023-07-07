using Octokit;
using Refit;

namespace StabilityMatrix.Core.Helper.Cache;

public interface IGithubApiCache
{
    Task<Release> GetLatestRelease(string username, string repository);
    
    Task<IOrderedEnumerable<Release>> GetAllReleases(string username, string repository);

    Task<IReadOnlyList<Branch>> GetAllBranches(string username, string repository);

    Task<IReadOnlyList<GitHubCommit>?> GetAllCommits(string username, string repository, string branch, int page = 1,
        [AliasAs("per_page")] int perPage = 10);
}
