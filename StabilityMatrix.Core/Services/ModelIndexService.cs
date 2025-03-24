using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
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
    private readonly SemaphoreSlim safetensorMetadataParseLock = new(1, 1);

    private DateTimeOffset lastUpdateCheck = DateTimeOffset.MinValue;

    private Dictionary<SharedFolderType, List<LocalModelFile>> _modelIndex = new();

    private HashSet<string>? _modelIndexBlake3Hashes;

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
            _ => liteDbContext.LocalModelFiles.FindAsync(m => types.Contains(m.SharedFolderType))
        };
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindByHashAsync(string hashBlake3)
    {
        return liteDbContext.LocalModelFiles.FindAsync(m => m.HashBlake3 == hashBlake3);
    }

    /// <inheritdoc />
    public Task RefreshIndex()
    {
        return RefreshIndexParallelCore();
    }

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
            if (!Enum.TryParse<SharedFolderType>(sharedFolderName, out var sharedFolderType))
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
                SharedFolderType = sharedFolderType
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
                .SupportedImageExtensions.Select(
                    ext => fileDirectory.JoinFile($"{fileNameWithoutExtension}.preview{ext}")
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

        using var db = await liteDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);

        var localModelFiles = db.GetCollection<LocalModelFile>("LocalModelFiles")!;

        await localModelFiles.DeleteAllAsync().ConfigureAwait(false);
        await localModelFiles.InsertBulkAsync(newIndexFlat).ConfigureAwait(false);

        await db.CommitAsync().ConfigureAwait(false);

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
            _ => 1
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
                if (!Enum.TryParse<SharedFolderType>(sharedFolderName, out var sharedFolderType))
                {
                    sharedFolderType = SharedFolderType.Unknown;
                }

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
                    SharedFolderType = sharedFolderType
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
                    .SupportedImageExtensions.Select(
                        ext => fileDirectory.JoinFile($"{fileNameWithoutExtension}.preview{ext}")
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

        using var db = await liteDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
        var localModelFiles = db.GetCollection<LocalModelFile>("LocalModelFiles")!;

        await localModelFiles.DeleteAllAsync().ConfigureAwait(false);
        await localModelFiles.InsertBulkAsync(newIndexComplete).ConfigureAwait(false);

        await db.CommitAsync().ConfigureAwait(false);

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

        lastUpdateCheck = DateTimeOffset.UtcNow;

        var installedHashes = ModelIndexBlake3Hashes;
        var dbModels = (
            await liteDbContext.LocalModelFiles.FindAllAsync().ConfigureAwait(false) ?? []
        ).ToList();

        var ids = dbModels
            .Where(x => x.ConnectedModelInfo?.ModelId != null)
            .Select(x => x.ConnectedModelInfo!.ModelId.Value)
            .Distinct();

        var remoteModels = (await modelFinder.FindRemoteModelsById(ids).ConfigureAwait(false)).ToList();

        // update the civitmodels cache with this new result
        await liteDbContext.UpsertCivitModelAsync(remoteModels).ConfigureAwait(false);

        var localModelsToUpdate = new List<LocalModelFile>();
        foreach (var dbModel in dbModels)
        {
            if (dbModel.ConnectedModelInfo == null)
                continue;

            var remoteModel = remoteModels.FirstOrDefault(m => m.Id == dbModel.ConnectedModelInfo!.ModelId);

            var latestVersion = remoteModel?.ModelVersions?.FirstOrDefault();

            if (latestVersion?.Files is not { } latestVersionFiles)
            {
                continue;
            }

            var latestHashes = latestVersionFiles
                .Where(f => f.Type == CivitFileType.Model)
                .Select(f => f.Hashes.BLAKE3)
                .Where(hash => hash is not null)
                .ToList();

            dbModel.HasUpdate = !latestHashes.Any(hash => installedHashes.Contains(hash!));
            dbModel.LastUpdateCheck = DateTimeOffset.UtcNow;
            dbModel.LatestModelInfo = remoteModel;

            localModelsToUpdate.Add(dbModel);
        }
        await liteDbContext.LocalModelFiles.UpsertAsync(localModelsToUpdate).ConfigureAwait(false);
        await LoadFromDbAsync().ConfigureAwait(false);
    }

    public async Task UpsertModelAsync(LocalModelFile model)
    {
        await liteDbContext.LocalModelFiles.UpsertAsync(model).ConfigureAwait(false);
        await LoadFromDbAsync().ConfigureAwait(false);
    }

    private void OnModelIndexReset()
    {
        _modelIndexBlake3Hashes = null;
    }

    private static HashSet<string> CollectModelHashes(IEnumerable<LocalModelFile> models)
    {
        var hashes = new HashSet<string>();
        foreach (var model in models)
        {
            if (model.ConnectedModelInfo?.Hashes?.BLAKE3 is { } hashBlake3)
            {
                hashes.Add(hashBlake3);
            }
        }
        return hashes;
    }
}
