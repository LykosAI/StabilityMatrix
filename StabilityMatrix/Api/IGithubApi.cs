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
}
