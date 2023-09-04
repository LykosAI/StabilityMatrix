using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteDB.Async;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockLiteDbContext : ILiteDbContext
{
    public LiteDatabaseAsync Database => throw new NotImplementedException();
    public ILiteCollectionAsync<CivitModel> CivitModels => throw new NotImplementedException();
    public ILiteCollectionAsync<CivitModelVersion> CivitModelVersions => throw new NotImplementedException();
    public ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache => throw new NotImplementedException();
    public ILiteCollectionAsync<LocalModelFile> LocalModelFiles => throw new NotImplementedException();

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

    public Task<GithubCacheEntry?> GetGithubCacheEntry(string cacheKey)
    {
        return Task.FromResult<GithubCacheEntry>(null);
    }

    public Task<bool> UpsertGithubCacheEntry(GithubCacheEntry cacheEntry)
    {
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
