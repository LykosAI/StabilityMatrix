using System.Collections.Immutable;
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

    // Tracks handled exceptions
    private readonly HashSet<HandledExceptionInfo> handledExceptions = [];

    private readonly Lazy<LiteDatabaseAsync> lazyDatabase;
    public LiteDatabaseAsync Database => lazyDatabase.Value;

    // Notification events
    public event EventHandler? CivitModelsChanged;

    // Collections (Tables)
    public ILiteCollectionAsync<CivitModel> CivitModels => Database.GetCollection<CivitModel>("CivitModels");
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
    public ILiteCollectionAsync<PyPiCacheEntry> PyPiCache =>
        Database.GetCollection<PyPiCacheEntry>("PyPiCache");
    public ILiteCollectionAsync<CivitBaseModelTypeCacheEntry> CivitBaseModelTypeCache =>
        Database.GetCollection<CivitBaseModelTypeCacheEntry>("CivitBaseModelTypeCache");

    public LiteDbContext(
        ILogger<LiteDbContext> logger,
        ISettingsManager settingsManager,
        IOptions<DebugOptions> debugOptions
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.debugOptions = debugOptions.Value;

        lazyDatabase = new Lazy<LiteDatabaseAsync>(CreateDatabase);
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
                    new ConnectionString() { Filename = dbPath, Connection = ConnectionType.Shared, }
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
        LiteDBExtensions.Register<CivitModel, CivitModelVersion>(m => m.ModelVersions, "CivitModelVersions");
        LiteDBExtensions.Register<CivitModelQueryCacheEntry, CivitModel>(e => e.Items, "CivitModels");
        LiteDBExtensions.Register<LocalModelFile, CivitModel>(e => e.LatestModelInfo, "CivitModels");

        return db;
    }

    public async Task<(CivitModel?, CivitModelVersion?)> FindCivitModelFromFileHashAsync(string hashBlake3)
    {
        var version = await CivitModelVersions
            .Query()
            .Where(
                mv =>
                    mv.Files!.Select(f => f.Hashes)
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

        return await TryQueryWithClearOnExceptionAsync(GithubCache, GithubCache.FindByIdAsync(cacheKey))
            .ConfigureAwait(false);
    }

    public Task<bool> UpsertGithubCacheEntry(GithubCacheEntry cacheEntry) =>
        GithubCache.UpsertAsync(cacheEntry);

    public async Task<PyPiCacheEntry?> GetPyPiCacheEntry(string? cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        return await TryQueryWithClearOnExceptionAsync(PyPiCache, PyPiCache.FindByIdAsync(cacheKey))
            .ConfigureAwait(false);
    }

    public Task<bool> UpsertPyPiCacheEntry(PyPiCacheEntry cacheEntry) => PyPiCache.UpsertAsync(cacheEntry);

    /// <summary>
    /// Clear all Collections that store re-fetchable cache type data.
    /// </summary>
    public async Task ClearAllCacheCollectionsAsync()
    {
        var collectionNames = new List<string>
        {
            nameof(CivitModels),
            nameof(CivitModelVersions),
            nameof(CivitModelQueryCache),
            nameof(GithubCache),
            nameof(LocalModelFiles),
            nameof(LocalImageFiles)
        };

        logger.LogInformation("Clearing all cache collections: [{@Names}]", collectionNames);

        foreach (var name in collectionNames)
        {
            var collection = Database.GetCollection(name);
            await collection.DeleteAllAsync().ConfigureAwait(false);
        }
    }

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
    public async Task<TResult?> TryQueryWithClearOnExceptionAsync<T, TResult>(
        ILiteCollectionAsync<T> collection,
        Task<TResult> task
    )
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var exceptionInfo = new HandledExceptionInfo(
                collection.Name,
                ex.ToString(),
                ex.InnerException?.ToString()
            );

            lock (handledExceptions)
            {
                var exceptionString = ex.InnerException is null
                    ? $"{ex.GetType()}"
                    : $"{ex.GetType()} ({ex.InnerException.GetType()})";

                // Throw if exception was already handled previously this session
                // then it's probably not a migration issue
                if (handledExceptions.Contains(exceptionInfo))
                {
                    throw new AggregateException(
                        $"Repeated LiteDb error '{exceptionString}' while fetching from '{exceptionInfo.CollectionName}', previously handled",
                        ex
                    );
                }

                // Log warning for known exception types, otherwise log error
                if (
                    ex is LiteException or LiteAsyncException
                    && ex.InnerException
                        is InvalidCastException // GitHub cache int type changes
                            or ArgumentException // Unknown enum values
                )
                {
                    logger.LogWarning(
                        ex,
                        "LiteDb error while fetching from {Name}, collection will be cleared: {Exception}",
                        collection.Name,
                        exceptionString
                    );
                }
                else
                {
#if DEBUG
                    throw;
#else
                    logger.LogError(
                        ex,
                        "LiteDb unknown error while fetching from {Name}, collection will be cleared: {Exception}",
                        collection.Name,
                        exceptionString
                    );
#endif
                }

                // Add to handled exceptions
                handledExceptions.Add(exceptionInfo);
            }

            // Clear collection
            await collection.DeleteAllAsync().ConfigureAwait(false);

            // Get referenced collections
            var referencedCollections = FindReferencedCollections(collection).ToArray();
            if (referencedCollections.Length > 0)
            {
                logger.LogWarning(
                    "Clearing referenced collections: [{@Names}]",
                    referencedCollections.Select(c => c.Name)
                );

                foreach (var referencedCollection in referencedCollections)
                {
                    await referencedCollection.DeleteAllAsync().ConfigureAwait(false);
                }
            }
        }

        return default;
    }

    public void Dispose()
    {
        if (lazyDatabase.IsValueCreated)
        {
            try
            {
                Database.Dispose();
            }
            catch (ObjectDisposedException) { }
            catch (ApplicationException)
            {
                // Ignores a mutex error from library
                // https://stability-matrix.sentry.io/share/issue/5c62f37462444e7eab18cea314af231f/
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Recursively find all referenced collections in the entity mapper of a collection.
    /// </summary>
    private IEnumerable<ILiteCollectionAsync<BsonDocument>> FindReferencedCollections<T>(
        ILiteCollectionAsync<T> collection
    )
    {
        var collectionNames = Database.UnderlyingDatabase.GetCollectionNames().ToArray();

        foreach (
            var referencedCollectionName in FindReferencedCollectionNamesRecursive(
                collection.EntityMapper,
                [collection.Name]
            )
        )
        {
            yield return Database.GetCollection(referencedCollectionName);
        }

        yield break;

        IEnumerable<string> FindReferencedCollectionNamesRecursive(
            EntityMapper entityMapper,
            ImmutableHashSet<string> seenCollectionNames
        )
        {
            foreach (var member in entityMapper.Members)
            {
                // Only look for members that are DBRef
                if (!member.IsDbRef || member.UnderlyingType is not { } dbRefType)
                    continue;

                // Skip if not a collection or already seen
                if (!collectionNames.Contains(dbRefType.Name) || seenCollectionNames.Contains(dbRefType.Name))
                    continue;

                var memberCollection = Database.GetCollection(dbRefType.Name);

                seenCollectionNames = seenCollectionNames.Add(memberCollection.Name);
                yield return memberCollection.Name;

                // Also recursively find references in the referenced collection
                foreach (
                    var subCollectionName in FindReferencedCollectionNamesRecursive(
                        memberCollection.EntityMapper,
                        seenCollectionNames
                    )
                )
                {
                    seenCollectionNames = seenCollectionNames.Add(subCollectionName);
                    yield return subCollectionName;
                }
            }
        }
    }

    public async Task<CivitBaseModelTypeCacheEntry?> GetCivitBaseModelTypeCacheEntry(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return await CivitBaseModelTypeCache.FindByIdAsync(id).ConfigureAwait(false);
    }

    public Task<bool> UpsertCivitBaseModelTypeCacheEntry(CivitBaseModelTypeCacheEntry entry) =>
        CivitBaseModelTypeCache.UpsertAsync(entry);

    private readonly record struct HandledExceptionInfo(
        string CollectionName,
        string Exception,
        string? InnerException
    );
}
