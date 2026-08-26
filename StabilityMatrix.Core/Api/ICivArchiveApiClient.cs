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
    /// Fetch the populated lists of selectable model types and base models via a
    /// parameterless request against the search route.
    /// </summary>
    Task<CivArchiveFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
    Task<CivArchiveModelDetailsResponse> GetModelDetailsAsync(
        string relativeUrl,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resolve a <c>/sha256/{hash}</c> URL (a File-kind search result) to the canonical
    /// <c>/models/{id}?modelVersionId={vid}</c> URL of the version that actually contains
    /// that file, plus a thumbnail image when the linked version has a gallery. The SHA256
    /// endpoint returns a different shape (linked models array) so File-kind results can't
    /// be loaded via <see cref="GetModelDetailsAsync"/> directly. Returns null when the
    /// hash isn't linked to any model.
    /// </summary>
    Task<CivArchiveFileResolution?> ResolveFileAsync(
        string sha256RelativeUrl,
        CancellationToken cancellationToken = default
    );

    Uri GetAbsoluteUri(string relativeUrl);
}
