using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB.Async;
using StabilityMatrix.Extensions;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Database;

public class LiteDbContext : ILiteDbContext
{
    public LiteDatabaseAsync Database { get; }

    // Notification events
    public event EventHandler? CivitModelsChanged;
    
    // Collections (Tables)
    public ILiteCollectionAsync<CivitModel> CivitModels => Database.GetCollection<CivitModel>("CivitModels");
    public ILiteCollectionAsync<CivitModelVersion> CivitModelVersions => Database.GetCollection<CivitModelVersion>("CivitModelVersions");
    public ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache => Database.GetCollection<CivitModelQueryCacheEntry>("CivitModelQueryCache");

    public LiteDbContext(string connectionString)
    {
        Database = new LiteDatabaseAsync(connectionString);

        LiteDBExtensions.Register<CivitModel, CivitModelVersion>(m => m.ModelVersions, "CivitModelVersions");
        LiteDBExtensions.Register<CivitModelQueryCacheEntry, CivitModel>(e => e.Items, "CivitModels");
    }
    
    public async Task<bool> UpsertCivitModelAsync(CivitModel civitModel)
    {
        // Insert model versions first
        var versions = civitModel.ModelVersions;
        await CivitModelVersions.UpsertAsync(versions);
        // Then insert the model
        var updated = await CivitModels.UpsertAsync(civitModel);
        // Notify listeners
        if (updated)
        {
            CivitModelsChanged?.Invoke(this, EventArgs.Empty);
        }

        return updated;
    }
    
    public async Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels)
    {
        var civitModelsArray = civitModels.ToArray();
        // Get all model versions
        var versions = civitModelsArray.SelectMany(model => model.ModelVersions);
        await CivitModelVersions.UpsertAsync(versions);
        // Then insert the models
        var updated = await CivitModels.UpsertAsync(civitModelsArray) > 0;
        // Notify listeners
        if (updated)
        {
            CivitModelsChanged?.Invoke(this, EventArgs.Empty);
        }
        return updated;
    }
    
    // Add to cache
    public async Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry)
    {
        var changed = await CivitModelQueryCache.UpsertAsync(entry);
        if (changed)
        {
            CivitModelsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }
}
