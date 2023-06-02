using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Refit;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Helper.Cache;

public interface IGithubApiCache
{
    Task<Release> GetLatestRelease(string username, string repository);
    
    Task<IOrderedEnumerable<Release>> GetAllReleases(string username, string repository);

    Task<IReadOnlyList<Branch>> GetAllBranches(string username, string repository);

    Task<IReadOnlyList<GitHubCommit>?> GetAllCommits(string username, string repository, string branch, int page = 1,
        [AliasAs("per_page")] int perPage = 10);
}
