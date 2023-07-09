using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteDB.Async;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockLiteDbContext : ILiteDbContext
{
    public LiteDatabaseAsync Database { get; }
    public ILiteCollectionAsync<CivitModel> CivitModels { get; }
    public ILiteCollectionAsync<CivitModelVersion> CivitModelVersions { get; }
    public ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache { get; }
    public Task<(CivitModel?, CivitModelVersion?)> FindCivitModelFromFileHashAsync(string hashBlake3)
    {
        return Task.FromResult<(CivitModel?, CivitModelVersion?)>((null, null));
    }

    public Task<bool> UpsertCivitModelAsync(CivitModel civitModel)
    {
        return Task.FromResult(true);
    }

    public Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels)
    {
        return Task.FromResult(true);
    }

    public Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry)
    {
        return Task.FromResult(true);
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
