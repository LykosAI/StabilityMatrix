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

    Task<(CivitModel?, CivitModelVersion?)> FindCivitModelFromFileHashAsync(string hashBlake3);
    Task<bool> UpsertCivitModelAsync(CivitModel civitModel);
    Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels);
    Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry);
    Task<GithubCacheEntry?> GetGithubCacheEntry(string cacheKey);
    Task<bool> UpsertGithubCacheEntry(GithubCacheEntry cacheEntry);
}
