using System.Diagnostics;
using System.Text;
using AsyncAwaitBestPractices;
using AutoCtor;
using Microsoft.Extensions.Logging;
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

    public Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; private set; } = new();

    [AutoPostConstruct]
    private void Initialize()
    {
        // Start background index when library dir is set
        settingsManager.RegisterOnLibraryDirSet(_ => BackgroundRefreshIndex());
    }

    public IEnumerable<LocalModelFile> GetFromModelIndex(SharedFolderType types)
    {
        return ModelIndex.Where(kvp => (kvp.Key & types) != 0).SelectMany(kvp => kvp.Value);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LocalModelFile>> FindAsync(SharedFolderType type)
    {
        await liteDbContext.LocalModelFiles.EnsureIndexAsync(m => m.SharedFolderType).ConfigureAwait(false);

        return await liteDbContext
            .LocalModelFiles.FindAsync(m => m.SharedFolderType == type)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LocalModelFile>> FindByHashAsync(string hashBlake3)
    {
        await liteDbContext.LocalModelFiles.EnsureIndexAsync(m => m.HashBlake3).ConfigureAwait(false);

        return await liteDbContext
            .LocalModelFiles.FindAsync(m => m.HashBlake3 == hashBlake3)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RefreshIndex()
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

        using var db = await liteDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);

        var localModelFiles = db.GetCollection<LocalModelFile>("LocalModelFiles")!;

        await localModelFiles.DeleteAllAsync().ConfigureAwait(false);

        // Record start of actual indexing
        var indexStart = stopwatch.Elapsed;

        var added = 0;

        var newIndex = new Dictionary<SharedFolderType, List<LocalModelFile>>();

        foreach (
            var file in modelsDir
                .Info.EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Select(info => new FilePath(info))
        )
        {
            // Skip if not supported extension
            if (!LocalModelFile.SupportedCheckpointExtensions.Contains(file.Info.Extension))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(modelsDir, file);

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
            var jsonPath = file.Directory!.JoinFile(
                new FilePath($"{file.NameWithoutExtension}.cm-info.json")
            );

            if (jsonPath.Exists)
            {
                var connectedModelInfo = ConnectedModelInfo.FromJson(
                    await jsonPath.ReadAllTextAsync().ConfigureAwait(false)
                );

                localModel.ConnectedModelInfo = connectedModelInfo;
            }

            // Try to find a preview image
            var previewImagePath = LocalModelFile
                .SupportedImageExtensions.Select(
                    ext => file.Directory!.JoinFile($"{file.NameWithoutExtension}.preview{ext}")
                )
                .FirstOrDefault(path => path.Exists);

            if (previewImagePath != null)
            {
                localModel.PreviewImageRelativePath = Path.GetRelativePath(modelsDir, previewImagePath);
            }

            // Try to find a config file (same name as model file but with .yaml extension)
            if (file.WithName($"{file.NameWithoutExtension}.yaml") is { Exists: true } configFile)
            {
                localModel.ConfigFullPath = configFile;
            }

            // Insert into database
            await localModelFiles.InsertAsync(localModel).ConfigureAwait(false);

            // Add to index
            var list = newIndex.GetOrAdd(sharedFolderType);
            list.Add(localModel);

            added++;
        }

        // Update index
        ModelIndex = newIndex;
        // Record end of actual indexing
        var indexEnd = stopwatch.Elapsed;

        await db.CommitAsync().ConfigureAwait(false);

        // End
        stopwatch.Stop();
        var indexDuration = indexEnd - indexStart;
        var dbDuration = stopwatch.Elapsed - indexDuration;

        logger.LogInformation(
            "Model index refreshed with {Entries} entries, took {IndexDuration:F1}ms ({DbDuration:F1}ms db)",
            added,
            indexDuration.TotalMilliseconds,
            dbDuration.TotalMilliseconds
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
