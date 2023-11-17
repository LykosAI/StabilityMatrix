using Octokit;
using Refit;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Helper.Cache;

public interface IGithubApiCache
{
    Task<IEnumerable<Release>> GetAllReleases(string username, string repository);

    Task<IEnumerable<Branch>> GetAllBranches(string username, string repository);

    Task<IEnumerable<GitCommit>?> GetAllCommits(
        string username,
        string repository,
        string branch,
        int page = 1,
        [AliasAs("per_page")] int perPage = 10
    );
}
