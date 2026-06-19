using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Core.Models.Api.HuggingFace;

namespace StabilityMatrix.Core.Api;

public interface IHuggingFaceApi
{
    [Get("/api/whoami-v2")]
    Task<IApiResponse<HuggingFaceUser>> GetCurrentUserAsync([Header("Authorization")] string authorization);

    /// <summary>
    /// Search models. Mirrors <c>GET /api/models</c>.
    /// </summary>
    /// <param name="search">Free-text search query.</param>
    /// <param name="sort">Sort field, e.g. <c>downloads</c>, <c>likes</c>, <c>lastModified</c>.</param>
    /// <param name="direction"><c>-1</c> for descending.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="filter">Optional tag filter (e.g. a pipeline tag).</param>
    /// <param name="authorization">Optional <c>Bearer {token}</c> header.</param>
    [Get("/api/models")]
    Task<IApiResponse<List<HuggingFaceSearchEntry>>> SearchModelsAsync(
        [Query] string search,
        [Query] string sort,
        [Query] int direction,
        [Query] int limit,
        [Query] string? filter,
        [Header("Authorization")] string? authorization,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// List the file tree of a model repository.
    /// Mirrors <c>GET /api/models/{owner}/{model}/tree/{revision}</c>.
    /// </summary>
    [Get("/api/models/{owner}/{model}/tree/{revision}")]
    Task<IApiResponse<List<HuggingFaceTreeEntry>>> GetRepoTreeAsync(
        string owner,
        string model,
        string revision,
        [Query] bool recursive,
        [Header("Authorization")] string? authorization,
        CancellationToken cancellationToken = default
    );
}
