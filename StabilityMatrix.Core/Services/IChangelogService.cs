using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Fetches and parses the Stability Matrix changelog markdown.
/// </summary>
public interface IChangelogService
{
    /// <summary>
    /// Fetch release notes from the given changelog url, formatted for the update dialog
    /// (Keep-a-Changelog markdown trimmed to versions up to and including the current version).
    /// </summary>
    Task<string> GetReleaseNotes(string changelogUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the full changelog for the user's preferred update channel and return the parsed
    /// version blocks, newest first, filtered to the preferred channel (including the current
    /// version and older). Falls back to the on-disk cache if the network fetch fails.
    /// Returns an empty list if neither the network nor cache is available.
    /// </summary>
    Task<IReadOnlyList<ChangelogEntry>> GetChangelogEntries(CancellationToken cancellationToken = default);

    /// <summary>
    /// Url to the changelog on GitHub, used as a user-facing fallback link.
    /// </summary>
    string GitHubChangelogUrl { get; }
}
