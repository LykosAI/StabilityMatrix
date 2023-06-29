using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using LiteDB.Async;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Database;

public interface ILiteDbContext : IDisposable
{
    LiteDatabaseAsync Database { get; }
    
    ILiteCollectionAsync<CivitModel> CivitModels { get; }
    ILiteCollectionAsync<CivitModelVersion> CivitModelVersions { get; }
    ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache { get; }

    void Initialize(string connectionString);
    
    Task<(CivitModel?, CivitModelVersion?)> FindCivitModelFromFileHashAsync(string hashBlake3);
    Task<bool> UpsertCivitModelAsync(CivitModel civitModel);
    Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels);
    Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry);
}
