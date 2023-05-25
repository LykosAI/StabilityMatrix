using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface IGithubApi
{
    [Get("/repos/{username}/{repository}/releases/latest")]
    Task<GithubRelease> GetLatestRelease(string username, string repository);
}
