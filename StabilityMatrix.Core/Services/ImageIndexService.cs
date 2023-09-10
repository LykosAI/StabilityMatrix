using System.Diagnostics;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

public class ImageIndexService : IImageIndexService
{
    private readonly ILogger<ImageIndexService> logger;
    private readonly ILiteDbContext liteDbContext;
    private readonly ISettingsManager settingsManager;

    public ImageIndexService(
        ILogger<ImageIndexService> logger,
        ILiteDbContext liteDbContext,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.liteDbContext = liteDbContext;
        this.settingsManager = settingsManager;
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

    /// <inheritdoc />
    public async Task RefreshIndex(string subPath = "")
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
            var metadata = ImageMetadata.ParseFile(file);

            var localImage = new LocalImageFile
            {
                RelativePath = relativePath,
                ImageType = imageType,
                CreatedAt = file.Info.CreationTimeUtc,
                LastModifiedAt = file.Info.LastWriteTimeUtc,
                GenerationParameters = metadata.GetGenerationParameters()
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
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        RefreshIndex().SafeFireAndForget();
    }

    /// <inheritdoc />
    public async Task RemoveImage(LocalImageFile imageFile)
    {
        await liteDbContext.LocalImageFiles
            .DeleteAsync(imageFile.RelativePath)
            .ConfigureAwait(false);
    }
}
