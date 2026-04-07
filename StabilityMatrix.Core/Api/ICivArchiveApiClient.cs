using StabilityMatrix.Core.Models.Api.CivArchive;

namespace StabilityMatrix.Core.Api;

public interface ICivArchiveApiClient
{
    Task<string> GetBuildIdAsync(CancellationToken cancellationToken = default);
    Task<CivArchiveSearchResponse> SearchAsync(
        CivArchiveSearchFilters filters,
        CancellationToken cancellationToken = default
    );
    Task<CivArchiveModelDetailsResponse> GetModelDetailsAsync(
        string relativeUrl,
        CancellationToken cancellationToken = default
    );
    Uri GetAbsoluteUri(string relativeUrl);
}
