using System.Collections.Concurrent;
using System.Diagnostics;
using AsyncAwaitBestPractices;
using DynamicData;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

[Singleton(typeof(IImageIndexService))]
public class ImageIndexService : IImageIndexService
{
    private readonly ILogger<ImageIndexService> logger;
    private readonly ISettingsManager settingsManager;

    /// <inheritdoc />
    public IndexCollection<LocalImageFile, string> InferenceImages { get; }

    public ImageIndexService(ILogger<ImageIndexService> logger, ISettingsManager settingsManager)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;

        InferenceImages = new IndexCollection<LocalImageFile, string>(
            this,
            file => file.AbsolutePath
        )
        {
            RelativePath = "Inference"
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

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        RefreshIndexForAllCollections().SafeFireAndForget();
    }
}
