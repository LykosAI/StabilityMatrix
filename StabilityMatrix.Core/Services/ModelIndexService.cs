using System.Diagnostics;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

public class ModelIndexService : IModelIndexService
{
    private readonly ILogger<ModelIndexService> logger;
    private readonly ILiteDbContext liteDbContext;
    private readonly ISettingsManager settingsManager;

    public Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; private set; } =
        new();

    public ModelIndexService(
        ILogger<ModelIndexService> logger,
        ILiteDbContext liteDbContext,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.liteDbContext = liteDbContext;
        this.settingsManager = settingsManager;
    }

    /// <summary>
    /// Deletes all entries in the local model file index.
    /// </summary>
    private async Task ClearIndex()
    {
        await liteDbContext.LocalModelFiles.DeleteAllAsync().ConfigureAwait(false);
    }

    public IEnumerable<LocalModelFile> GetFromModelIndex(SharedFolderType types)
    {
        return ModelIndex.Where(kvp => (kvp.Key & types) != 0).SelectMany(kvp => kvp.Value);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalModelFile>> GetModelsOfType(SharedFolderType type)
    {
        return await liteDbContext.LocalModelFiles
            .Query()
            .Where(m => m.SharedFolderType == type)
            .ToArrayAsync()
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
            var file in modelsDir.Info
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
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

            var localModel = new LocalModelFile
            {
                RelativePath = relativePath,
                SharedFolderType = sharedFolderType,
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
            var previewImagePath = LocalModelFile.SupportedImageExtensions
                .Select(
                    ext => file.Directory!.JoinFile($"{file.NameWithoutExtension}.preview{ext}")
                )
                .FirstOrDefault(path => path.Exists);

            if (previewImagePath != null)
            {
                localModel.PreviewImageRelativePath = Path.GetRelativePath(
                    modelsDir,
                    previewImagePath
                );
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
}
