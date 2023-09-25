using LiteDB;
using LiteDB.Async;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Database;

public class LiteDbContext : ILiteDbContext
{
    private readonly ILogger<LiteDbContext> logger;
    private readonly ISettingsManager settingsManager;
    private readonly DebugOptions debugOptions;

    private LiteDatabaseAsync? database;
    public LiteDatabaseAsync Database => database ??= CreateDatabase();

    // Notification events
    public event EventHandler? CivitModelsChanged;

    // Collections (Tables)
    public ILiteCollectionAsync<CivitModel> CivitModels =>
        Database.GetCollection<CivitModel>("CivitModels");
    public ILiteCollectionAsync<CivitModelVersion> CivitModelVersions =>
        Database.GetCollection<CivitModelVersion>("CivitModelVersions");
    public ILiteCollectionAsync<CivitModelQueryCacheEntry> CivitModelQueryCache =>
        Database.GetCollection<CivitModelQueryCacheEntry>("CivitModelQueryCache");
    public ILiteCollectionAsync<GithubCacheEntry> GithubCache =>
        Database.GetCollection<GithubCacheEntry>("GithubCache");
    public ILiteCollectionAsync<LocalModelFile> LocalModelFiles =>
        Database.GetCollection<LocalModelFile>("LocalModelFiles");
    public ILiteCollectionAsync<InferenceProjectEntry> InferenceProjects =>
        Database.GetCollection<InferenceProjectEntry>("InferenceProjects");
    public ILiteCollectionAsync<LocalImageFile> LocalImageFiles =>
        Database.GetCollection<LocalImageFile>("LocalImageFiles");

    public LiteDbContext(
        ILogger<LiteDbContext> logger,
        ISettingsManager settingsManager,
        IOptions<DebugOptions> debugOptions
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.debugOptions = debugOptions.Value;
    }

    private LiteDatabaseAsync CreateDatabase()
    {
        LiteDatabaseAsync? db = null;

        if (debugOptions.TempDatabase)
        {
            db = new LiteDatabaseAsync(":temp:");
        }
        else
        {
            // Attempt to create connection, might be in use
            try
            {
                var dbPath = Path.Combine(settingsManager.LibraryDir, "StabilityMatrix.db");
                db = new LiteDatabaseAsync(
                    new ConnectionString()
                    {
                        Filename = dbPath,
                        Connection = ConnectionType.Shared,
                    }
                );
            }
            catch (IOException e)
            {
                logger.LogWarning(
                    "Database in use or not accessible ({Message}), using temporary database",
                    e.Message
                );
            }
        }

        // Fallback to temporary database
        db ??= new LiteDatabaseAsync(":temp:");

        // Register reference fields
        LiteDBExtensions.Register<CivitModel, CivitModelVersion>(
            m => m.ModelVersions,
            "CivitModelVersions"
        );
        LiteDBExtensions.Register<CivitModelQueryCacheEntry, CivitModel>(
            e => e.Items,
            "CivitModels"
        );

        return db;
    }

    public async Task<(CivitModel?, CivitModelVersion?)> FindCivitModelFromFileHashAsync(
        string hashBlake3
    )
    {
        var version = await CivitModelVersions
            .Query()
            .Where(
                mv =>
                    mv.Files!
                        .Select(f => f.Hashes)
                        .Select(hashes => hashes.BLAKE3)
                        .Any(hash => hash == hashBlake3)
            )
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (version is null)
            return (null, null);

        var model = await CivitModels
            .Query()
            .Include(m => m.ModelVersions)
            .Where(m => m.ModelVersions!.Select(v => v.Id).Any(id => id == version.Id))
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return (model, version);
    }

    public async Task<bool> UpsertCivitModelAsync(CivitModel civitModel)
    {
        // Insert model versions first then model
        var versionsUpdated = await CivitModelVersions
            .UpsertAsync(civitModel.ModelVersions)
            .ConfigureAwait(false);
        var updated = await CivitModels.UpsertAsync(civitModel).ConfigureAwait(false);
        // Notify listeners on any change
        var anyUpdated = versionsUpdated > 0 || updated;
        if (anyUpdated)
        {
            CivitModelsChanged?.Invoke(this, EventArgs.Empty);
        }
        return anyUpdated;
    }

    public async Task<bool> UpsertCivitModelAsync(IEnumerable<CivitModel> civitModels)
    {
        var civitModelsArray = civitModels.ToArray();
        // Get all model versions then insert models
        var versions = civitModelsArray.SelectMany(model => model.ModelVersions ?? new());
        var versionsUpdated = await CivitModelVersions.UpsertAsync(versions).ConfigureAwait(false);
        var updated = await CivitModels.UpsertAsync(civitModelsArray).ConfigureAwait(false);
        // Notify listeners on any change
        var anyUpdated = versionsUpdated > 0 || updated > 0;
        if (updated > 0 || versionsUpdated > 0)
        {
            CivitModelsChanged?.Invoke(this, EventArgs.Empty);
        }
        return anyUpdated;
    }

    // Add to cache
    public async Task<bool> UpsertCivitModelQueryCacheEntryAsync(CivitModelQueryCacheEntry entry)
    {
        var changed = await CivitModelQueryCache.UpsertAsync(entry).ConfigureAwait(false);
        if (changed)
        {
            CivitModelsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public async Task<GithubCacheEntry?> GetGithubCacheEntry(string? cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        if (await GithubCache.FindByIdAsync(cacheKey).ConfigureAwait(false) is { } result)
        {
            return result;
        }

        return null;
    }

    public Task<bool> UpsertGithubCacheEntry(GithubCacheEntry cacheEntry) =>
        GithubCache.UpsertAsync(cacheEntry);

    public void Dispose()
    {
        if (database is not null)
        {
            try
            {
                database.Dispose();
            }
            catch (ObjectDisposedException) { }

            database = null;
        }

        GC.SuppressFinalize(this);
    }
}
