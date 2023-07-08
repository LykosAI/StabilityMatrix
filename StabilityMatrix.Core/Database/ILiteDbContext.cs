using LiteDB.Async;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Database;

public interface ILiteDbContext : IDisposable
{
    LiteDatabaseAsync Database { get; }
    
    ILiteCollectionAsync<CivitModel> CivitModels { get; }
    ILiteCollectionAsync<CivitModelVersion> CivitModelVersions { get; }
    ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache { get; }

    
    Task<(CivitModel?, CivitModelVersion?)> FindCivitModelFromFileHashAsync(string hashBlake3);
    Task<bool> UpsertCivitModelAsync(CivitModel civitModel);
    Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels);
    Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry);
}
