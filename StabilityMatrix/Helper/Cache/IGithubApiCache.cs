using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Helper.Cache;

public interface IGithubApiCache
{
    Task<GithubRelease> GetLatestRelease(string username, string repository);
    
    Task<IEnumerable<GithubRelease>> GetAllReleases(string username, string repository);

    Task<IEnumerable<GithubBranch>> GetAllBranches(string username, string repository);

    Task<IEnumerable<GithubCommit>> GetAllCommits(string username, string repository, string branch, int page = 1,
        [AliasAs("per_page")] int perPage = 10);
}
