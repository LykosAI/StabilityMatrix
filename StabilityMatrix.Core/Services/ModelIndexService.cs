using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using AsyncAwaitBestPractices;
using AutoCtor;
using Injectio.Attributes;
using KGySoft.CoreLibraries;
using LiteDB;
using LiteDB.Async;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<IModelIndexService, ModelIndexService>]
[AutoConstruct]
public partial class ModelIndexService : IModelIndexService
{
    private readonly ILogger<ModelIndexService> logger;
    private readonly ISettingsManager settingsManager;
    private readonly ILiteDbContext liteDbContext;
    private readonly ModelFinder modelFinder;

    private DateTimeOffset lastUpdateCheck = DateTimeOffset.MinValue;

    private Dictionary<SharedFolderType, List<LocalModelFile>> _modelIndex = new();

    private HashSet<string>? _modelIndexBlake3Hashes;
    private HashSet<string>? _modelIndexSha256Hashes;
    private HashSet<string>? _modelIndexCivArchiveUrls;

    /// <summary>
    /// Whether the database has been initially loaded.
    /// </summary>
    private bool IsDbLoaded { get; set; }

    public Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex
    {
        get => _modelIndex;
        private set
        {
            _modelIndex = value;
            OnModelIndexReset();
        }
    }

    public IReadOnlySet<string> ModelIndexBlake3Hashes =>
        _modelIndexBlake3Hashes ??= CollectModelHashes(ModelIndex.Values.SelectMany(x => x));

    public IReadOnlySet<string> ModelIndexSha256Hashes =>
        _modelIndexSha256Hashes ??= CollectModelSha256Hashes(ModelIndex.Values.SelectMany(x => x));

    public IReadOnlySet<string> ModelIndexCivArchiveUrls =>
        _modelIndexCivArchiveUrls ??= CollectCivArchiveUrls(ModelIndex.Values.SelectMany(x => x));

    [AutoPostConstruct]
    private void Initialize()
    {
        // Start background index when library dir is set
        settingsManager.RegisterOnLibraryDirSet(_ =>
        {
            // Skip if already loaded
            if (IsDbLoaded)
            {
                return;
            }

            Task.Run(async () =>
                {
                    // Build db indexes
                    await liteDbContext
                        .LocalModelFiles.EnsureIndexAsync(m => m.HashBlake3)
                        .ConfigureAwait(false);
                    await liteDbContext
                        .LocalModelFiles.EnsureIndexAsync(m => m.SharedFolderType)
                        .ConfigureAwait(false);

                    // Load models first from db, then do index refresh
                    await EnsureLoadedAsync().ConfigureAwait(false);

                    await RefreshIndex().ConfigureAwait(false);
                })
                .SafeFireAndForget(ex =>
                {
                    logger.LogError(ex, "Error loading model index");
                });
        });
    }

    // Ensure the in memory cache is loaded
    private async Task EnsureLoadedAsync()
    {
        if (!IsDbLoaded)
        {
            await LoadFromDbAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Populates <see cref="ModelIndex"/> from the database.
    /// </summary>
    private async Task LoadFromDbAsync()
    {
        var timer = Stopwatch.StartNew();

        logger.LogInformation("Loading models from database...");

        // Handle enum deserialize exceptions from changes
        var allModels = await liteDbContext
            .TryQueryWithClearOnExceptionAsync(
                liteDbContext.LocalModelFiles,
                liteDbContext.LocalModelFiles.IncludeAll().FindAllAsync()
            )
            .ConfigureAwait(false);

        if (allModels is not null)
        {
            ModelIndex = allModels.GroupBy(m => m.SharedFolderType).ToDictionary(g => g.Key, g => g.ToList());
        }
        else
        {
            ModelIndex.Clear();
        }

        IsDbLoaded = true;
        EventManager.Instance.OnModelIndexChanged();

        timer.Stop();
        logger.LogInformation(
            "Loaded {Count} models from database in {Time:F2}ms",
            ModelIndex.Count,
            timer.Elapsed.TotalMilliseconds
        );
    }

    /// <inheritdoc />
    public async Task<Dictionary<SharedFolderType, LocalModelFolder>> FindAllFolders()
    {
        var modelFiles = await liteDbContext.LocalModelFiles.FindAllAsync().ConfigureAwait(false);

        var rootFolders = new Dictionary<SharedFolderType, LocalModelFolder>();

        foreach (var modelFile in modelFiles)
        {
            var pathParts = modelFile.RelativePath.Split(Path.DirectorySeparatorChar);
            var currentFolder = rootFolders.GetOrAdd(
                modelFile.SharedFolderType,
                _ => new LocalModelFolder { RelativePath = pathParts[0] }
            );
            for (var i = 1; i < pathParts.Length - 1; i++)
            {
                var folderName = pathParts[i];
                var folder = currentFolder.Folders.GetValueOrDefault(folderName);
                if (folder == null)
                {
                    folder = new LocalModelFolder { RelativePath = folderName };
                    currentFolder.Folders[folderName] = folder;
                }

                currentFolder = folder;
            }

            currentFolder.Files[modelFile.RelativePath] = modelFile;
        }

        return rootFolders;
    }

    /// <inheritdoc />
    public IEnumerable<LocalModelFile> FindByModelType(SharedFolderType types)
    {
        return ModelIndex.Where(kvp => (kvp.Key & types) != 0).SelectMany(kvp => kvp.Value);
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindByModelTypeAsync(SharedFolderType type)
    {
        // To list of types
        var types = Enum.GetValues<SharedFolderType>()
            .Where(folderType => type.HasFlag(folderType))
            .ToArray();

        return types.Length switch
        {
            0 => Task.FromResult(Enumerable.Empty<LocalModelFile>()),
            1 => liteDbContext.LocalModelFiles.FindAsync(m => m.SharedFolderType == type),
            _ => liteDbContext.LocalModelFiles.FindAsync(m => types.Contains(m.SharedFolderType)),
        };
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindByHashAsync(string hashBlake3)
    {
        return liteDbContext.LocalModelFiles.FindAsync(m => m.HashBlake3 == hashBlake3);
    }

    public Task<IEnumerable<LocalModelFile>> FindBySha256Async(string hashSha256)
    {
        return liteDbContext.LocalModelFiles.FindAsync(m => m.HashSha256 == hashSha256);
    }

    /// <inheritdoc />
    public Task RefreshIndex()
    {
        return RefreshIndexParallelCore();
    }

    /// <summary>
    /// Resolves a top-level models folder name to its <see cref="SharedFolderType"/>.
    /// Case-insensitive, so a folder whose casing diverges from the canonical name (possible on
    /// case-sensitive file systems, e.g. "textencoders" next to "TextEncoders") still indexes as
    /// its canonical type. Unmatched names resolve to <see cref="SharedFolderType.Unknown"/>.
    /// </summary>
    internal static SharedFolderType ParseSharedFolderType(string folderName) =>
        Enum.TryParse<SharedFolderType>(folderName, ignoreCase: true, out var type)
            ? type
            : SharedFolderType.Unknown;

    /// <summary>
    /// Filters models so no two entries collide on the <see cref="LocalModelFile.RelativePath"/>
    /// primary key under the database's collation. The main database uses Ordinal collation, where
    /// distinct paths never collide; under a case-insensitive collation, paths differing only in
    /// case (possible on case-sensitive file systems) would otherwise fail the bulk insert with a
    /// duplicate key error.
    /// </summary>
    internal static IReadOnlyList<LocalModelFile> DeduplicateForDbCollation(
        IReadOnlyCollection<LocalModelFile> models,
        Collation collation,
        ILogger logger
    )
    {
        var deduplicated = new List<LocalModelFile>(models.Count);
        var seenPaths = new HashSet<string>(models.Count, GetKeyComparer(collation));

        foreach (var model in models)
        {
            if (seenPaths.Add(model.RelativePath))
            {
                deduplicated.Add(model);
            }
            else
            {
                logger.LogWarning(
                    "Skipping model {Path}: its path collides with an already indexed model "
                        + "under the database collation ({Collation})",
                    model.RelativePath,
                    collation
                );
            }
        }

        return deduplicated;
    }

    /// <summary>
    /// A <see cref="StringComparer"/> with the same equality semantics as a LiteDB collation,
    /// for detecting primary key collisions ahead of insert.
    /// </summary>
    internal static StringComparer GetKeyComparer(Collation collation) =>
        collation.SortOptions switch
        {
            CompareOptions.Ordinal => StringComparer.Ordinal,
            CompareOptions.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase,
            var options => StringComparer.Create(
                collation.Culture,
                options.HasFlag(CompareOptions.IgnoreCase)
            ),
        };

    private async Task RefreshIndexCore()
    {
        if (!settingsManager.IsLibraryDirSet)
        {
            logger.LogTrace("Model index refresh skipped, library directory not set");
            return;
        }

        if (new DirectoryPath(settingsManager.ModelsDirectory) is not { Exists: true } modelsDir)
        {
            logger.LogTrace("Model index refresh skipped, model directory does not exist");
            return;
        }

        logger.LogInformation("Refreshing model index...");

        // Start
        var stopwatch = Stopwatch.StartNew();

        var newIndex = new Dictionary<SharedFolderType, List<LocalModelFile>>();
        var newIndexFlat = new List<LocalModelFile>();

        var paths = Directory
            .EnumerateFiles(modelsDir, "*", EnumerationOptionConstants.AllDirectories)
            .ToHashSet();

        foreach (var path in paths)
        {
            // Skip if not supported extension
            if (!LocalModelFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(modelsDir, path);

            // Get shared folder name
            var sharedFolderName = relativePath.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries
            )[0];
            // Try Convert to enum
            var sharedFolderType = ParseSharedFolderType(sharedFolderName);
            if (sharedFolderType is SharedFolderType.Unknown)
            {
                continue;
            }

            // Since RelativePath is the database key, for LiteDB this is limited to 1021 bytes
            if (Encoding.UTF8.GetByteCount(relativePath) is var byteCount and > 1021)
            {
                logger.LogWarning(
                    "Skipping model {Path} because it's path is too long ({Length} bytes)",
                    relativePath,
                    byteCount
                );

                continue;
            }

            var localModel = new LocalModelFile
            {
                RelativePath = relativePath,
                SharedFolderType = sharedFolderType,
            };

            // Try to find a connected model info
            var fileDirectory = new DirectoryPath(Path.GetDirectoryName(path)!);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            var jsonPath = fileDirectory.JoinFile($"{fileNameWithoutExtension}.cm-info.json");

            if (paths.Contains(jsonPath))
            {
                try
                {
                    await using var stream = jsonPath.Info.OpenRead();

                    var connectedModelInfo = await JsonSerializer
                        .DeserializeAsync(
                            stream,
                            ConnectedModelInfoSerializerContext.Default.ConnectedModelInfo
                        )
                        .ConfigureAwait(false);

                    localModel.ConnectedModelInfo = connectedModelInfo;
                }
                catch (Exception e)
                {
                    logger.LogWarning(
                        e,
                        "Failed to deserialize connected model info for {Path}, skipping",
                        jsonPath
                    );
                }
            }

            // Try to find a preview image
            var previewImagePath = LocalModelFile
                .SupportedImageExtensions.Select(ext =>
                    fileDirectory.JoinFile($"{fileNameWithoutExtension}.preview{ext}")
                )
                .FirstOrDefault(filePath => paths.Contains(filePath));

            if (previewImagePath is not null)
            {
                localModel.PreviewImageRelativePath = Path.GetRelativePath(modelsDir, previewImagePath);
            }

            // Try to find a config file (same name as model file but with .yaml extension)
            var configFile = fileDirectory.JoinFile($"{fileNameWithoutExtension}.yaml");
            if (paths.Contains(configFile))
            {
                localModel.ConfigFullPath = configFile;
            }

            // Add to index
            newIndexFlat.Add(localModel);
            var list = newIndex.GetOrAdd(sharedFolderType);
            list.Add(localModel);
        }

        ModelIndex = newIndex;

        stopwatch.Stop();
        var indexTime = stopwatch.Elapsed;

        // Insert to db as transaction
        stopwatch.Restart();

        var dbModels = DeduplicateForDbCollation(newIndexFlat, liteDbContext.Database.Collation, logger);

        try
        {
            using var db = await liteDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);

            var localModelFiles = db.GetCollection<LocalModelFile>("LocalModelFiles")!;

            await localModelFiles.DeleteAllAsync().ConfigureAwait(false);
            await localModelFiles.InsertBulkAsync(dbModels).ConfigureAwait(false);

            await db.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception e) when (e is LiteException or LiteAsyncException)
        {
            // A failed persist must not propagate: callers await refreshes from UI contexts where
            // an exception crashes the app, and the in-memory index above is already updated.
            logger.LogError(e, "Failed to persist model index to database");
        }

        stopwatch.Stop();
        var dbTime = stopwatch.Elapsed;

        logger.LogInformation(
            "Model index refreshed with {Entries} entries, took (index: {IndexDuration}), (db: {DbDuration})",
            newIndexFlat.Count,
            CodeTimer.FormatTime(indexTime),
            CodeTimer.FormatTime(dbTime)
        );

        EventManager.Instance.OnModelIndexChanged();
    }

    private async Task RefreshIndexParallelCore()
    {
        if (!settingsManager.IsLibraryDirSet)
        {
            logger.LogTrace("Model index refresh skipped, library directory not set");
            return;
        }

        if (new DirectoryPath(settingsManager.ModelsDirectory) is not { Exists: true } modelsDir)
        {
            logger.LogTrace("Model index refresh skipped, model directory does not exist");
            return;
        }

        // Start
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Refreshing model index...");

        var newIndexFlat = new ConcurrentBag<LocalModelFile>();

        var paths = Directory
            .EnumerateFiles(modelsDir, "*", EnumerationOptionConstants.AllDirectories)
            .ToHashSet();

        var partitioner = Partitioner.Create(paths, EnumerablePartitionerOptions.NoBuffering);

        var numThreads = Environment.ProcessorCount switch
        {
            >= 20 => Environment.ProcessorCount / 3 - 1,
            > 1 => Environment.ProcessorCount,
            _ => 1,
        };

        Parallel.ForEach(
            partitioner,
            new ParallelOptions { MaxDegreeOfParallelism = numThreads },
            path =>
            {
                // Skip if not supported extension
                if (!LocalModelFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(path)))
                {
                    return;
                }

                var relativePath = Path.GetRelativePath(modelsDir, path);

                // Get shared folder name
                var sharedFolderName = relativePath.Split(
                    Path.DirectorySeparatorChar,
                    StringSplitOptions.RemoveEmptyEntries
                )[0];
                // Try Convert to enum
                var sharedFolderType = ParseSharedFolderType(sharedFolderName);

                // Since RelativePath is the database key, for LiteDB this is limited to 1021 bytes
                if (Encoding.UTF8.GetByteCount(relativePath) is var byteCount and > 1021)
                {
                    logger.LogWarning(
                        "Skipping model {Path} because it's path is too long ({Length} bytes)",
                        relativePath,
                        byteCount
                    );

                    return;
                }

                var localModel = new LocalModelFile
                {
                    RelativePath = relativePath,
                    SharedFolderType = sharedFolderType,
                };

                // Try to find a connected model info
                var fileDirectory = new DirectoryPath(Path.GetDirectoryName(path)!);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                var jsonPath = fileDirectory.JoinFile($"{fileNameWithoutExtension}.cm-info.json");

                if (paths.Contains(jsonPath))
                {
                    try
                    {
                        using var stream = jsonPath.Info.OpenRead();

                        var connectedModelInfo = JsonSerializer.Deserialize(
                            stream,
                            ConnectedModelInfoSerializerContext.Default.ConnectedModelInfo
                        );

                        // Seems there is a limitation of LiteDB datetime resolution, so drop nanoseconds on load
                        // Otherwise new loaded models with ns will cause mismatching equality with models loaded from db with no ns
                        if (connectedModelInfo?.ImportedAt is { } importedAt && importedAt.Nanosecond != 0)
                        {
                            connectedModelInfo.ImportedAt = new DateTimeOffset(
                                importedAt.Year,
                                importedAt.Month,
                                importedAt.Day,
                                importedAt.Hour,
                                importedAt.Minute,
                                importedAt.Second,
                                importedAt.Millisecond,
                                importedAt.Offset
                            );
                        }

                        localModel.ConnectedModelInfo = connectedModelInfo;
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(
                            e,
                            "Failed to deserialize connected model info for {Path}, skipping",
                            jsonPath
                        );
                    }
                }

                // Try to find a preview image
                var previewImagePath = LocalModelFile
                    .SupportedImageExtensions.Select(ext =>
                        fileDirectory.JoinFile($"{fileNameWithoutExtension}.preview{ext}")
                    )
                    .FirstOrDefault(filePath => paths.Contains(filePath));

                if (previewImagePath is not null)
                {
                    localModel.PreviewImageRelativePath = Path.GetRelativePath(modelsDir, previewImagePath);
                }

                // Try to find a config file (same name as model file but with .yaml extension)
                var configFile = fileDirectory.JoinFile($"{fileNameWithoutExtension}.yaml");
                if (paths.Contains(configFile))
                {
                    localModel.ConfigFullPath = configFile;
                }

                // Add to index
                newIndexFlat.Add(localModel);
            }
        );

        var newIndexComplete = newIndexFlat.ToArray();

        var modelsDict = ModelIndex
            .Values.SelectMany(x => x)
            .DistinctBy(f => f.RelativePath)
            .ToDictionary(f => f.RelativePath, file => file);

        var newIndex = new Dictionary<SharedFolderType, List<LocalModelFile>>();
        foreach (var model in newIndexComplete)
        {
            if (modelsDict.TryGetValue(model.RelativePath, out var dbModel))
            {
                model.HasUpdate = dbModel.HasUpdate;
                model.HasEarlyAccessUpdateOnly = dbModel.HasEarlyAccessUpdateOnly;
                model.LastUpdateCheck = dbModel.LastUpdateCheck;
                model.LatestModelInfo = dbModel.LatestModelInfo;
            }

            if (model.LatestModelInfo == null && model.HasCivitMetadata)
            {
                // Handle enum deserialize exceptions from changes
                if (
                    await liteDbContext
                        .TryQueryWithClearOnExceptionAsync(
                            liteDbContext.CivitModels,
                            liteDbContext
                                .CivitModels.Include(m => m.ModelVersions)
                                .FindByIdAsync(model.ConnectedModelInfo.ModelId)
                        )
                        .ConfigureAwait(false) is
                    { } latestModel
                )
                {
                    model.LatestModelInfo = latestModel;
                }
            }
            var list = newIndex.GetOrAdd(model.SharedFolderType);
            list.Add(model);
        }

        ModelIndex = newIndex;

        stopwatch.Stop();
        var indexTime = stopwatch.Elapsed;

        // Insert to db as transaction
        stopwatch.Restart();

        var dbModels = DeduplicateForDbCollation(newIndexComplete, liteDbContext.Database.Collation, logger);

        try
        {
            using var db = await liteDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
            var localModelFiles = db.GetCollection<LocalModelFile>("LocalModelFiles")!;

            await localModelFiles.DeleteAllAsync().ConfigureAwait(false);
            await localModelFiles.InsertBulkAsync(dbModels).ConfigureAwait(false);

            await db.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception e) when (e is LiteException or LiteAsyncException)
        {
            // A failed persist must not propagate: callers await refreshes from UI contexts where
            // an exception crashes the app, and the in-memory index above is already updated.
            logger.LogError(e, "Failed to persist model index to database");
        }

        stopwatch.Stop();
        var dbTime = stopwatch.Elapsed;

        logger.LogInformation(
            "Model index refreshed with {Entries} entries, took {IndexDuration} ({DbDuration} db)",
            newIndexFlat.Count,
            CodeTimer.FormatTime(indexTime),
            CodeTimer.FormatTime(dbTime)
        );

        EventManager.Instance.OnModelIndexChanged();
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        Task.Run(async () => await RefreshIndex().ConfigureAwait(false))
            .SafeFireAndForget(ex =>
            {
                logger.LogError(ex, "Error in background model indexing");
            });
    }

    /// <inheritdoc />
    public async Task<bool> RemoveModelAsync(LocalModelFile model)
    {
        // Remove from database
        if (await liteDbContext.LocalModelFiles.DeleteAsync(model.RelativePath).ConfigureAwait(false))
        {
            // Remove from index
            if (ModelIndex.TryGetValue(model.SharedFolderType, out var list))
            {
                list.RemoveAll(x => x.RelativePath == model.RelativePath);

                OnModelIndexReset();
                EventManager.Instance.OnModelIndexChanged();
            }

            return true;
        }

        return false;
    }

    public async Task<bool> RemoveModelsAsync(IEnumerable<LocalModelFile> models)
    {
        var modelsList = models.ToList();
        var paths = modelsList.Select(m => m.RelativePath).ToList();
        var result = true;

        foreach (var path in paths)
        {
            result &= await liteDbContext.LocalModelFiles.DeleteAsync(path).ConfigureAwait(false);
        }

        foreach (var model in modelsList)
        {
            if (ModelIndex.TryGetValue(model.SharedFolderType, out var list))
            {
                list.RemoveAll(x => x.RelativePath == model.RelativePath);
            }
        }

        OnModelIndexReset();
        EventManager.Instance.OnModelIndexChanged();

        return result;
    }

    public async Task CheckModelsForUpdateAsync()
    {
        if (DateTimeOffset.UtcNow < lastUpdateCheck.AddMinutes(5))
        {
            return;
        }

        var installedHashes = ModelIndexBlake3Hashes;
        var dbModels = (
            await liteDbContext.LocalModelFiles.FindAllAsync().ConfigureAwait(false) ?? []
        ).ToList();

        var ids = dbModels
            .Where(x => x.ConnectedModelInfo?.ModelId != null)
            .Select(x => x.ConnectedModelInfo!.ModelId.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            lastUpdateCheck = DateTimeOffset.UtcNow;
            return;
        }

        var remoteModels = (await modelFinder.FindRemoteModelsById(ids).ConfigureAwait(false)).ToList();

        // An empty result for a non-empty id set means the API was unreachable; keep the
        // existing flags and leave the throttle window unconsumed so the next visit retries.
        if (remoteModels.Count == 0)
        {
            return;
        }

        lastUpdateCheck = DateTimeOffset.UtcNow;

        // update the civitmodels cache with this new result
        await liteDbContext.UpsertCivitModelAsync(remoteModels).ConfigureAwait(false);

        var localModelsToUpdate = new List<LocalModelFile>();
        foreach (var dbModel in dbModels)
        {
            if (dbModel.ConnectedModelInfo == null)
                continue;

            var remoteModel = remoteModels.FirstOrDefault(m => m.Id == dbModel.ConnectedModelInfo!.ModelId);

            // Absent from the response (removed from CivitAI or a partially failed batch):
            // indeterminate, so keep the previous flags rather than inventing a change.
            if (remoteModel == null)
                continue;

            dbModel.HasUpdate = ComputeHasUpdate(dbModel, remoteModel, installedHashes);
            dbModel.HasEarlyAccessUpdateOnly = GetHasEarlyAccessUpdateOnly(dbModel, remoteModel);
            dbModel.LastUpdateCheck = DateTimeOffset.UtcNow;
            dbModel.LatestModelInfo = remoteModel;

            localModelsToUpdate.Add(dbModel);
        }
        await liteDbContext.LocalModelFiles.UpsertAsync(localModelsToUpdate).ConfigureAwait(false);
        await LoadFromDbAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Decides whether a newer installable version of a model exists on CivitAI. Prefers hash
    /// evidence (any of the latest version's files already installed somewhere in the library);
    /// falls back to version-id position when the latest version publishes no hashable files.
    /// Indeterminate cases resolve to false so we never show an update badge we can't substantiate.
    /// Multi-architecture models publish parallel version tracks in one list (v7_Illustrious,
    /// v1_Anima, v4_Pony, ...), so comparisons stay within the installed file's base-model track —
    /// a release for a different architecture is a cross-grade, not an update.
    /// </summary>
    private static bool ComputeHasUpdate(
        LocalModelFile model,
        CivitModel remoteModel,
        IReadOnlySet<string> installedHashes
    )
    {
        if (
            FilterToInstalledTrack(remoteModel.ModelVersions, model.ConnectedModelInfo?.BaseModel)
            is not { Count: > 0 } versions
        )
            return false;

        var latestVersion = versions[0];
        var installedVersionId = model.ConnectedModelInfo?.VersionId;

        if (installedVersionId != null && installedVersionId == latestVersion.Id)
            return false;

        var latestHashes = (latestVersion.Files ?? [])
            .Where(f => f.Type.IsDownloadableModelFile())
            .Select(f => f.Hashes?.BLAKE3)
            .Where(hash => !string.IsNullOrEmpty(hash))
            .ToList();

        if (latestHashes.Count > 0)
        {
            return !latestHashes.Any(hash => installedHashes.Contains(hash!));
        }

        // No hash evidence — flag only when the installed version verifiably sits below the
        // latest in the published version list.
        return installedVersionId != null && versions.FindIndex(v => v.Id == installedVersionId.Value) > 0;
    }

    /// <summary>
    /// Narrows a model's version list to the installed file's base-model track. Falls back to
    /// the full list when the installed base model is unknown or matches nothing (e.g. the
    /// track was renamed or delisted) rather than reporting nothing forever.
    /// </summary>
    private static List<CivitModelVersion>? FilterToInstalledTrack(
        List<CivitModelVersion>? versions,
        string? installedBaseModel
    )
    {
        if (versions is null || string.IsNullOrWhiteSpace(installedBaseModel))
            return versions;

        var track = versions
            .Where(v => string.Equals(v.BaseModel, installedBaseModel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return track.Count > 0 ? track : versions;
    }

    public async Task UpsertModelAsync(LocalModelFile model)
    {
        await liteDbContext.LocalModelFiles.UpsertAsync(model).ConfigureAwait(false);
        await LoadFromDbAsync().ConfigureAwait(false);
    }

    private void OnModelIndexReset()
    {
        _modelIndexBlake3Hashes = null;
        _modelIndexSha256Hashes = null;
        _modelIndexCivArchiveUrls = null;
    }

    private static HashSet<string> CollectModelHashes(IEnumerable<LocalModelFile> models)
    {
        // CivitAI reports BLAKE3 uppercase while locally computed hashes are lowercase
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in models)
        {
            if (model.ConnectedModelInfo?.Hashes?.BLAKE3 is { } hashBlake3)
            {
                hashes.Add(hashBlake3);
            }
        }
        return hashes;
    }

    private static HashSet<string> CollectModelSha256Hashes(IEnumerable<LocalModelFile> models)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in models)
        {
            if (!string.IsNullOrWhiteSpace(model.HashSha256))
            {
                hashes.Add(model.HashSha256);
            }
        }
        return hashes;
    }

    private static HashSet<string> CollectCivArchiveUrls(IEnumerable<LocalModelFile> models)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in models)
        {
            if (model.HasCivArchiveMetadata && !string.IsNullOrWhiteSpace(model.ConnectedModelInfo.SourceUrl))
            {
                urls.Add(model.ConnectedModelInfo.SourceUrl);
            }
        }
        return urls;
    }

    private static bool GetHasEarlyAccessUpdateOnly(LocalModelFile model, CivitModel? remoteModel)
    {
        if (!model.HasUpdate || !model.HasCivitMetadata)
            return false;

        var versions = FilterToInstalledTrack(
            remoteModel?.ModelVersions,
            model.ConnectedModelInfo?.BaseModel
        );
        if (versions == null || versions.Count == 0)
            return false;

        var installedVersionId = model.ConnectedModelInfo?.VersionId;
        if (installedVersionId == null)
            return false;

        var installedIndex = versions.FindIndex(version => version.Id == installedVersionId.Value);
        if (installedIndex == 0)
            return false;

        // When the installed version no longer appears in the published list, every published
        // version is a potential update; the badge is early-access-only when all of them are.
        var newerVersions = installedIndex > 0 ? versions.Take(installedIndex) : versions;
        return newerVersions.All(version => version.IsEarlyAccess);
    }
}
