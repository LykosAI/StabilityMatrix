using StabilityMatrix.Core.Models.Api.CivArchive;

namespace StabilityMatrix.Core.Api;

public interface ICivArchiveApiClient
{
    Task<string> GetBuildIdAsync(CancellationToken cancellationToken = default);
    Task<CivArchiveSearchResponse> SearchAsync(
        CivArchiveSearchFilters filters,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Fetch the populated lists of selectable model types and base models. CivArchive's
    /// Next.js endpoint only returns these arrays when called with no query string at all —
    /// any filter param (even <c>platform=all</c>) causes the server to return empty
    /// arrays. This method makes the parameterless request so the multi-select dropdowns
    /// can actually be populated.
    /// </summary>
    Task<CivArchiveFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
    Task<CivArchiveModelDetailsResponse> GetModelDetailsAsync(
        string relativeUrl,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resolve a <c>/sha256/{hash}</c> URL (a File-kind search result) to the canonical
    /// <c>/models/{id}?modelVersionId={vid}</c> URL of the version that actually contains
    /// that file. The SHA256 endpoint returns a different shape (linked models array) so
    /// File-kind results can't be loaded via <see cref="GetModelDetailsAsync"/> directly.
    /// Returns null when the hash isn't linked to any model.
    /// </summary>
    Task<string?> ResolveFileUrlAsync(
        string sha256RelativeUrl,
        CancellationToken cancellationToken = default
    );

    Uri GetAbsoluteUri(string relativeUrl);
}
