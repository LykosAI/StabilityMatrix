using LiteDB.Async;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Database;

public interface ILiteDbContext : IDisposable
{
    LiteDatabaseAsync Database { get; }

    ILiteCollectionAsync<CivitModel> CivitModels { get; }
    ILiteCollectionAsync<CivitModelVersion> CivitModelVersions { get; }
    ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache { get; }
    ILiteCollectionAsync<LocalModelFile> LocalModelFiles { get; }
    ILiteCollectionAsync<InferenceProjectEntry> InferenceProjects { get; }
    ILiteCollectionAsync<LocalImageFile> LocalImageFiles { get; }
    ILiteCollectionAsync<CivitBaseModelTypeCacheEntry> CivitBaseModelTypeCache { get; }

    Task<(CivitModel?, CivitModelVersion?)> FindCivitModelFromFileHashAsync(string hashBlake3);
    Task<bool> UpsertCivitModelAsync(CivitModel civitModel);
    Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels);
    Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry);
    Task<GithubCacheEntry?> GetGithubCacheEntry(string cacheKey);
    Task<bool> UpsertGithubCacheEntry(GithubCacheEntry cacheEntry);

    /// <summary>
    /// Clear all Collections that store re-fetchable cache type data.
    /// </summary>
    Task ClearAllCacheCollectionsAsync();

    /// <summary>
    /// Executes a query with exception logging and collection clearing.
    /// This will handle unique exceptions once keyed by string representation for each collection,
    /// and throws if repeated.
    /// </summary>
    /// <typeparam name="T">The type of collection to query.</typeparam>
    /// <typeparam name="TResult">The type of result to return.</typeparam>
    /// <param name="collection">The collection to query.</param>
    /// <param name="task">The task representing the query to execute.</param>
    /// <returns>The result of the query, or default value on handled exception.</returns>
    Task<TResult?> TryQueryWithClearOnExceptionAsync<T, TResult>(
        ILiteCollectionAsync<T> collection,
        Task<TResult> task
    );

    Task<PyPiCacheEntry?> GetPyPiCacheEntry(string? cacheKey);
    Task<bool> UpsertPyPiCacheEntry(PyPiCacheEntry cacheEntry);

    Task<CivitBaseModelTypeCacheEntry?> GetCivitBaseModelTypeCacheEntry(string id);
    Task<bool> UpsertCivitBaseModelTypeCacheEntry(CivitBaseModelTypeCacheEntry entry);
}
