using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using AsyncAwaitBestPractices;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

public class ImageIndexService : IImageIndexService
{
    private readonly ILogger<ImageIndexService> logger;
    private readonly ILiteDbContext liteDbContext;
    private readonly ISettingsManager settingsManager;

    /// <inheritdoc />
    public IndexCollection<LocalImageFile, string> InferenceImages { get; }

    public ImageIndexService(
        ILogger<ImageIndexService> logger,
        ILiteDbContext liteDbContext,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.liteDbContext = liteDbContext;
        this.settingsManager = settingsManager;

        InferenceImages = new IndexCollection<LocalImageFile, string>(
            this,
            file => file.RelativePath
        )
        {
            RelativePath = "inference"
        };

        EventManager.Instance.ImageFileAdded += OnImageFileAdded;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalImageFile>> GetLocalImagesByPrefix(string pathPrefix)
    {
        return await liteDbContext.LocalImageFiles
            .Query()
            .Where(imageFile => imageFile.RelativePath.StartsWith(pathPrefix))
            .ToArrayAsync()
            .ConfigureAwait(false);
    }

    public async Task RefreshIndexForAllCollections()
    {
        await RefreshIndex(InferenceImages).ConfigureAwait(false);
    }

    public async Task RefreshIndex(IndexCollection<LocalImageFile, string> indexCollection)
    {
        if (indexCollection.RelativePath is not { } subPath)
            return;

        var imagesDir = settingsManager.ImagesDirectory;
        var searchDir = imagesDir.JoinDir(indexCollection.RelativePath);
        if (!searchDir.Exists)
        {
            return;
        }

        // Start
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Refreshing images index at {ImagesDir}...", imagesDir);

        var toAdd = new ConcurrentBag<LocalImageFile>();

        await Task.Run(() =>
            {
                var files = imagesDir.Info
                    .EnumerateFiles("*.*", SearchOption.AllDirectories)
                    .Where(info => LocalImageFile.SupportedImageExtensions.Contains(info.Extension))
                    .Select(info => new FilePath(info));

                Parallel.ForEach(
                    files,
                    f =>
                    {
                        toAdd.Add(LocalImageFile.FromPath(f));
                    }
                );
            })
            .ConfigureAwait(false);

        var indexElapsed = stopwatch.Elapsed;

        indexCollection.ItemsSource.EditDiff(toAdd, LocalImageFile.Comparer);

        // End
        stopwatch.Stop();
        var editElapsed = stopwatch.Elapsed - indexElapsed;

        logger.LogInformation(
            "Image index updated for {Prefix} with {Entries} files, took {IndexDuration:F1}ms ({EditDuration:F1}ms edit)",
            subPath,
            toAdd.Count,
            indexElapsed.TotalMilliseconds,
            editElapsed.TotalMilliseconds
        );
    }

    private void OnImageFileAdded(object? sender, FilePath filePath)
    {
        var fullPath = settingsManager.ImagesDirectory.JoinDir(InferenceImages.RelativePath!);

        if (!string.IsNullOrEmpty(Path.GetRelativePath(fullPath, filePath)))
        {
            InferenceImages.Add(LocalImageFile.FromPath(filePath));
        }
    }

    /*public async Task RefreshIndex(IndexCollection<LocalImageFile, string> indexCollection)
    {
        var imagesDir = settingsManager.ImagesDirectory;
        if (!imagesDir.Exists)
        {
            return;
        }

        // Start
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Refreshing images index...");

        using var db = await liteDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);

        var localImageFiles = db.GetCollection<LocalImageFile>("LocalImageFiles")!;

        await localImageFiles.DeleteAllAsync().ConfigureAwait(false);

        // Record start of actual indexing
        var indexStart = stopwatch.Elapsed;

        var added = 0;

        foreach (
            var file in imagesDir.Info
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(info => LocalImageFile.SupportedImageExtensions.Contains(info.Extension))
                .Select(info => new FilePath(info))
        )
        {
            var relativePath = Path.GetRelativePath(imagesDir, file);

            // Skip if not in sub-path
            if (!string.IsNullOrEmpty(subPath) && !relativePath.StartsWith(subPath))
            {
                continue;
            }

            // TODO: Support other types
            const LocalImageFileType imageType =
                LocalImageFileType.Inference | LocalImageFileType.TextToImage;

            // Get metadata
            using var reader = new BinaryReader(new FileStream(file.FullPath, FileMode.Open));
            var metadata = ImageMetadata.ReadTextChunk(reader, "parameters-json");
            GenerationParameters? genParams = null;

            if (!string.IsNullOrWhiteSpace(metadata))
            {
                genParams = JsonSerializer.Deserialize<GenerationParameters>(metadata);
            }
            else
            {
                metadata = ImageMetadata.ReadTextChunk(reader, "parameters");
                if (!string.IsNullOrWhiteSpace(metadata))
                {
                    GenerationParameters.TryParse(metadata, out genParams);
                }
            }

            var localImage = new LocalImageFile
            {
                RelativePath = relativePath,
                ImageType = imageType,
                CreatedAt = file.Info.CreationTimeUtc,
                LastModifiedAt = file.Info.LastWriteTimeUtc,
                GenerationParameters = genParams
            };

            // Insert into database
            await localImageFiles.InsertAsync(localImage).ConfigureAwait(false);

            added++;
        }
        // Record end of actual indexing
        var indexEnd = stopwatch.Elapsed;

        await db.CommitAsync().ConfigureAwait(false);

        // End
        stopwatch.Stop();
        var indexDuration = indexEnd - indexStart;
        var dbDuration = stopwatch.Elapsed - indexDuration;

        logger.LogInformation(
            "Image index updated for {Prefix} with {Entries} files, took {IndexDuration:F1}ms ({DbDuration:F1}ms db)",
            subPath,
            added,
            indexDuration.TotalMilliseconds,
            dbDuration.TotalMilliseconds
        );
    }*/

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        RefreshIndexForAllCollections().SafeFireAndForget();
    }

    /// <inheritdoc />
    public async Task RemoveImage(LocalImageFile imageFile)
    {
        await liteDbContext.LocalImageFiles
            .DeleteAsync(imageFile.RelativePath)
            .ConfigureAwait(false);
    }
}
