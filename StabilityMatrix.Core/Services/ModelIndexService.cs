using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AsyncAwaitBestPractices;
using AutoCtor;
using KGySoft.CoreLibraries;
using Microsoft.Extensions.Logging;
using Polly.Retry;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

[Singleton(typeof(IModelIndexService))]
[AutoConstruct]
public partial class ModelIndexService : IModelIndexService
{
    private readonly ILogger<ModelIndexService> logger;
    private readonly ISettingsManager settingsManager;
    private readonly ILiteDbContext liteDbContext;
    private readonly ModelFinder modelFinder;

    /// <summary>
    /// Whether the database has been initially loaded.
    /// </summary>
    private bool IsDbLoaded { get; set; }

    public Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; private set; } = new();

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

        var allModels = (
            await liteDbContext.LocalModelFiles.FindAllAsync().ConfigureAwait(false)
        ).ToImmutableArray();

        ModelIndex = allModels.GroupBy(m => m.SharedFolderType).ToDictionary(g => g.Key, g => g.ToList());

        IsDbLoaded = true;

        timer.Stop();
        logger.LogInformation(
            "Loaded {Count} models from database in {Time:F2}ms",
            allModels.Length,
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

        var paths = Directory.EnumerateFiles(modelsDir, "*.*", SearchOption.AllDirectories).ToHashSet();

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

        var paths = Directory.EnumerateFiles(modelsDir, "*.*", SearchOption.AllDirectories).ToHashSet();

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
                    return;
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

        var newIndex = new Dictionary<SharedFolderType, List<LocalModelFile>>();
        foreach (var model in newIndexComplete)
        {
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
                list.Remove(model);
            }

            EventManager.Instance.OnModelIndexChanged();

            return true;
        }

        return false;
    }

    // idk do somethin with this
    public async Task CheckModelsForUpdateAsync()
    {
        var installedHashes = settingsManager.Settings.InstalledModelHashes;
        var dbModels = (
            await liteDbContext.LocalModelFiles.FindAllAsync().ConfigureAwait(false)
            ?? Enumerable.Empty<LocalModelFile>()
        ).ToList();
        var ids = dbModels
            .Where(x => x.ConnectedModelInfo != null)
            .Where(
                x => x.LastUpdateCheck == default || x.LastUpdateCheck < DateTimeOffset.UtcNow.AddHours(-8)
            )
            .Select(x => x.ConnectedModelInfo!.ModelId);
        var remoteModels = (await modelFinder.FindRemoteModelsById(ids).ConfigureAwait(false)).ToList();

        foreach (var dbModel in dbModels)
        {
            if (dbModel.ConnectedModelInfo == null)
                continue;

            var remoteModel = remoteModels.FirstOrDefault(m => m.Id == dbModel.ConnectedModelInfo!.ModelId);

            var latestVersion = remoteModel?.ModelVersions?.FirstOrDefault();
            var latestHashes = latestVersion
                ?.Files
                ?.Where(f => f.Type == CivitFileType.Model)
                .Select(f => f.Hashes.BLAKE3);

            if (latestHashes == null)
                continue;

            dbModel.HasUpdate = !latestHashes.Any(hash => installedHashes?.Contains(hash) ?? false);
            dbModel.LastUpdateCheck = DateTimeOffset.UtcNow;

            await liteDbContext.LocalModelFiles.UpsertAsync(dbModel).ConfigureAwait(false);
        }
    }
}
