using Refit;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Api;

/// <summary>
/// Minimal GitHub REST API surface used by the in-app documentation viewer to
/// discover the file listing under the docs folder.
/// Base address: <c>https://api.github.com</c>.
/// </summary>
[Headers("User-Agent: StabilityMatrix", "Accept: application/vnd.github+json")]
public interface IGitHubContentApi
{
    /// <summary>
    /// Gets a git tree recursively. Pass <c>recursive=1</c> to include all nested entries.
    /// </summary>
    [Get("/repos/{owner}/{repo}/git/trees/{treeSha}")]
    Task<GitHubTreeResponse> GetTree(
        string owner,
        string repo,
        string treeSha,
        [Query] [AliasAs("recursive")] int recursive = 1,
        CancellationToken cancellationToken = default
    );
}
