using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface IGithubApi
{
    [Get("/repos/{username}/{repository}/releases/latest")]
    Task<GithubRelease> GetLatestRelease(string username, string repository);
    
    [Get("/repos/{username}/{repository}/releases")]
    Task<IEnumerable<GithubRelease>> GetAllReleases(string username, string repository);

    [Get("/repos/{username}/{repository}/branches")]
    Task<IEnumerable<GithubBranch>> GetAllBranches(string username, string repository);

    [Get("/repos/{username}/{repository}/commits?sha={branch}")]
    Task<IEnumerable<GithubCommit>> GetAllCommits(string username, string repository, string branch, int page = 1,
        [AliasAs("per_page")] int perPage = 10);
}
