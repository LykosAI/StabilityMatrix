using System.Collections.Concurrent;
using System.Diagnostics;
using AsyncAwaitBestPractices;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<IImageIndexService, ImageIndexService>]
public class ImageIndexService : IImageIndexService
{
    private readonly ILogger<ImageIndexService> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IVideoThumbnailService videoThumbnailService;

    /// <inheritdoc />
    public IndexCollection<LocalImageFile, string> InferenceImages { get; }

    public ImageIndexService(
        ILogger<ImageIndexService> logger,
        ISettingsManager settingsManager,
        IVideoThumbnailService videoThumbnailService
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.videoThumbnailService = videoThumbnailService;

        InferenceImages = new IndexCollection<LocalImageFile, string>(this, file => file.AbsolutePath)
        {
            RelativePath = "Inference",
        };

        EventManager.Instance.ImageFileAdded += OnImageFileAdded;
    }

    public Task RefreshIndexForAllCollections()
    {
        return RefreshIndex(InferenceImages);
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
        logger.LogInformation("Refreshing images index at {SearchDir}...", searchDir.ToString());

        var errors = 0;
        var toAdd = new ConcurrentBag<LocalImageFile>();

        await Task.Run(() =>
            {
                var files = searchDir
                    .EnumerateFiles("*", EnumerationOptionConstants.AllDirectories)
                    .Where(file =>
                        LocalImageFile.SupportedImageExtensions.Contains(file.Extension)
                        || LocalImageFile.SupportedVideoExtensions.Contains(file.Extension)
                    )
                    .Where(file => !file.FullPath.Contains(".sm-thumbs")); // Exclude video thumbnail directories

                Parallel.ForEach(files, f =>
                {
                    try
                    {
                        toAdd.Add(CreateIndexedFile(f));
                    }
                    catch (Exception e)
                    {
                        Interlocked.Increment(ref errors);
                        logger.LogWarning(
                            e,
                            "Failed to add indexed image file at {Path}, skipping",
                            f.FullPath
                        );
                    }
                });
            })
            .ConfigureAwait(false);

        var indexElapsed = stopwatch.Elapsed;

        indexCollection.ItemsSource.EditDiff(toAdd);

        // End
        stopwatch.Stop();
        var editElapsed = stopwatch.Elapsed - indexElapsed;

        logger.LogInformation(
            "Image index updated for {Prefix} with ({Added}/{Total}) files, took {IndexDuration:F1}ms ({EditDuration:F1}ms edit)",
            subPath,
            toAdd.Count,
            toAdd.Count + errors,
            indexElapsed.TotalMilliseconds,
            editElapsed.TotalMilliseconds
        );
    }

    private void OnImageFileAdded(object? sender, FilePath filePath)
    {
        var imagesFolder = settingsManager.ImagesDirectory.JoinDir(InferenceImages.RelativePath!);

        if (string.IsNullOrEmpty(Path.GetRelativePath(imagesFolder, filePath)))
        {
            logger.LogWarning(
                "Image file {Path} added outside of relative directory {DirPath}, skipping",
                filePath,
                imagesFolder
            );
            return;
        }

        try
        {
            InferenceImages.Add(CreateIndexedFile(filePath));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to add image file at {Path}", filePath);
        }
    }

    /// <summary>
    /// Builds the index entry for a file. Videos carry their poster frame path when one already exists;
    /// none is generated here, so indexing stays cheap.
    /// </summary>
    private LocalImageFile CreateIndexedFile(FilePath filePath)
    {
        var file = LocalImageFile.FromPath(filePath);

        return file.IsVideo
            ? file with
            {
                ThumbnailPath = videoThumbnailService.GetExistingThumbnailPath(filePath.FullPath),
            }
            : file;
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        RefreshIndexForAllCollections().SafeFireAndForget();
    }
}
