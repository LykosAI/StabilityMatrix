using System.Collections.Generic;
using System.Threading.Tasks;
using LiteDB.Async;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Database;

public interface ILiteDbContext
{
    LiteDatabaseAsync Database { get; }
    
    ILiteCollectionAsync<CivitModel> CivitModels { get; }
    ILiteCollectionAsync<CivitModelVersion> CivitModelVersions { get; }
    ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache { get; }
    
    Task<bool> UpsertCivitModelAsync(CivitModel civitModel);
    Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels);
    Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry);
}
