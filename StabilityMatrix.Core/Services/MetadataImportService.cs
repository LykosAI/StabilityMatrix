using System.Diagnostics;
using System.Text.Json;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Services;

[RegisterTransient<IMetadataImportService, MetadataImportService>]
public class MetadataImportService(
    ILogger<MetadataImportService> logger,
    IDownloadService downloadService,
    ModelFinder modelFinder
) : IMetadataImportService
{
    public async Task ScanDirectoryForMissingInfo(
        DirectoryPath directory,
        IProgress<ProgressReport>? progress = null
    )
    {
        progress?.Report(new ProgressReport(-1f, message: "Scanning directory...", isIndeterminate: true));

        var checkpointsWithoutMetadata = directory
            .EnumerateFiles("*", EnumerationOptionConstants.AllDirectories)
            .Where(FileHasNoCmInfo)
            .ToList();

        var scanned = 0;
        var success = 0;

        foreach (var checkpointFilePath in checkpointsWithoutMetadata)
        {
            if (scanned == 0)
            {
                progress?.Report(
                    new ProgressReport(
                        current: scanned,
                        total: checkpointsWithoutMetadata.Count,
                        message: "Scanning directory..."
                    )
                );
            }
            else
            {
                progress?.Report(
                    new ProgressReport(
                        current: scanned,
                        total: checkpointsWithoutMetadata.Count,
                        message: $"{success} files imported successfully"
                    )
                );
            }

            var fileNameWithoutExtension = checkpointFilePath.NameWithoutExtension;
            var cmInfoPath = checkpointFilePath.Directory?.JoinFile(
                $"{fileNameWithoutExtension}.cm-info.json"
            );
            var cmInfoExists = File.Exists(cmInfoPath);
            if (cmInfoExists)
                continue;

            var hashProgress = new Progress<ProgressReport>(report =>
            {
                progress?.Report(
                    new ProgressReport(
                        current: report.Current ?? 0,
                        total: report.Total ?? 0,
                        message: $"Scanning file {scanned}/{checkpointsWithoutMetadata.Count} ... {report.Percentage}%",
                        printToConsole: false
                    )
                );
            });

            try
            {
                var blake3 = await GetBlake3Hash(cmInfoPath, checkpointFilePath, hashProgress)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(blake3))
                {
                    logger.LogWarning($"Blake3 hash was null for {checkpointFilePath}");
                    scanned++;
                    continue;
                }

                var modelInfo = await modelFinder.RemoteFindModel(blake3).ConfigureAwait(false);
                if (modelInfo == null)
                {
                    logger.LogWarning($"Could not find model for {blake3}");
                    scanned++;
                    continue;
                }

                var (model, modelVersion, modelFile) = modelInfo.Value;

                var updatedCmInfo = new ConnectedModelInfo(
                    model,
                    modelVersion,
                    modelFile,
                    DateTimeOffset.UtcNow
                );
                await updatedCmInfo
                    .SaveJsonToDirectory(checkpointFilePath.Directory, fileNameWithoutExtension)
                    .ConfigureAwait(false);

                var image = modelVersion.Images?.FirstOrDefault(
                    img =>
                        LocalModelFile.SupportedImageExtensions.Contains(Path.GetExtension(img.Url))
                        && img.Type == "image"
                );
                if (image == null)
                {
                    scanned++;
                    success++;
                    continue;
                }

                await DownloadImage(image, checkpointFilePath, progress).ConfigureAwait(false);

                scanned++;
                success++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while scanning {checkpointFilePath}", checkpointFilePath);
                scanned++;
            }
        }

        progress?.Report(
            new ProgressReport(
                current: scanned,
                total: checkpointsWithoutMetadata.Count,
                message: $"Metadata found for {success}/{checkpointsWithoutMetadata.Count} files"
            )
        );
    }

    private static bool FileHasNoCmInfo(FilePath file)
    {
        return LocalModelFile.SupportedCheckpointExtensions.Contains(file.Extension)
            && !File.Exists(file.Directory?.JoinFile($"{file.NameWithoutExtension}.cm-info.json"));
    }

    public async Task UpdateExistingMetadata(
        DirectoryPath directory,
        IProgress<ProgressReport>? progress = null
    )
    {
        progress?.Report(new ProgressReport(-1f, message: "Scanning directory...", isIndeterminate: true));

        var cmInfoList = new Dictionary<FilePath, ConnectedModelInfo>();
        foreach (
            var cmInfoPath in directory.EnumerateFiles(
                "*.cm-info.json",
                EnumerationOptionConstants.AllDirectories
            )
        )
        {
            ConnectedModelInfo? cmInfo;
            try
            {
                cmInfo = JsonSerializer.Deserialize<ConnectedModelInfo>(
                    await cmInfoPath.ReadAllTextAsync().ConfigureAwait(false)
                );
            }
            catch (JsonException)
            {
                cmInfo = null;
            }
            if (cmInfo == null)
                continue;

            cmInfoList.Add(cmInfoPath, cmInfo);
        }

        var success = 1;
        foreach (var (filePath, cmInfoValue) in cmInfoList)
        {
            progress?.Report(
                new ProgressReport(
                    current: success,
                    total: cmInfoList.Count,
                    message: $"Updating metadata {success}/{cmInfoList.Count}"
                )
            );

            try
            {
                var hash = cmInfoValue.Hashes.BLAKE3;
                if (string.IsNullOrWhiteSpace(hash))
                    continue;

                var modelInfo = await modelFinder.RemoteFindModel(hash).ConfigureAwait(false);
                if (modelInfo == null)
                {
                    logger.LogWarning($"Could not find model for {hash}");
                    continue;
                }

                var (model, modelVersion, modelFile) = modelInfo.Value;

                var updatedCmInfo = new ConnectedModelInfo(
                    model,
                    modelVersion,
                    modelFile,
                    DateTimeOffset.UtcNow
                );

                var nameWithoutCmInfo = filePath.NameWithoutExtension.Replace(".cm-info", string.Empty);
                await updatedCmInfo
                    .SaveJsonToDirectory(filePath.Directory, nameWithoutCmInfo)
                    .ConfigureAwait(false);

                var image = modelVersion.Images?.FirstOrDefault(
                    img =>
                        LocalModelFile.SupportedImageExtensions.Contains(Path.GetExtension(img.Url))
                        && img.Type == "image"
                );
                if (image == null)
                    continue;

                await DownloadImage(image, filePath, progress).ConfigureAwait(false);

                success++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while updating {filePath}", filePath);
            }
        }
    }

    public async Task<ConnectedModelInfo?> GetMetadataForFile(
        FilePath filePath,
        IProgress<ProgressReport>? progress = null,
        bool forceReimport = false
    )
    {
        progress?.Report(new ProgressReport(-1f, message: "Getting metadata...", isIndeterminate: true));

        var fileNameWithoutExtension = filePath.NameWithoutExtension;
        var cmInfoPath = filePath.Directory?.JoinFile($"{fileNameWithoutExtension}.cm-info.json");
        var cmInfoExists = File.Exists(cmInfoPath);
        if (cmInfoExists && !forceReimport)
            return null;

        var hashProgress = new Progress<ProgressReport>(report =>
        {
            progress?.Report(
                new ProgressReport(
                    current: report.Current ?? 0,
                    total: report.Total ?? 0,
                    message: $"Getting metadata for {fileNameWithoutExtension} ... {report.Percentage}%",
                    printToConsole: false
                )
            );
        });
        var blake3 = await GetBlake3Hash(cmInfoPath, filePath, hashProgress).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(blake3))
        {
            logger.LogWarning($"Blake3 hash was null for {filePath}");
            return null;
        }

        var modelInfo = await modelFinder.RemoteFindModel(blake3).ConfigureAwait(false);
        if (modelInfo == null)
        {
            logger.LogWarning($"Could not find model for {blake3}");
            return null;
        }

        var (model, modelVersion, modelFile) = modelInfo.Value;

        var updatedCmInfo = new ConnectedModelInfo(model, modelVersion, modelFile, DateTimeOffset.UtcNow);
        await updatedCmInfo
            .SaveJsonToDirectory(filePath.Directory, fileNameWithoutExtension)
            .ConfigureAwait(false);

        var image = modelVersion.Images?.FirstOrDefault(
            img =>
                LocalModelFile.SupportedImageExtensions.Contains(Path.GetExtension(img.Url))
                && img.Type == "image"
        );

        if (image == null)
            return updatedCmInfo;

        var imagePath = await DownloadImage(image, filePath, progress).ConfigureAwait(false);
        updatedCmInfo.ThumbnailImageUrl = imagePath;

        return updatedCmInfo;
    }

    private static async Task<string?> GetBlake3Hash(
        FilePath? cmInfoPath,
        FilePath checkpointFilePath,
        IProgress<ProgressReport> hashProgress
    )
    {
        if (string.IsNullOrWhiteSpace(cmInfoPath?.ToString()) || !File.Exists(cmInfoPath))
        {
            return await FileHash.GetBlake3Async(checkpointFilePath, hashProgress).ConfigureAwait(false);
        }

        var cmInfo = JsonSerializer.Deserialize<ConnectedModelInfo>(
            await cmInfoPath.ReadAllTextAsync().ConfigureAwait(false)
        );
        return cmInfo?.Hashes.BLAKE3;
    }

    private async Task<string> DownloadImage(
        CivitImage image,
        FilePath modelFilePath,
        IProgress<ProgressReport>? progress
    )
    {
        var imageExt = Path.GetExtension(image.Url).TrimStart('.');
        var nameWithoutCmInfo = modelFilePath.NameWithoutExtension.Replace(".cm-info", string.Empty);
        var imageDownloadPath = Path.GetFullPath(
            Path.Combine(modelFilePath.Directory, $"{nameWithoutCmInfo}.preview.{imageExt}")
        );
        await downloadService
            .DownloadToFileAsync(image.Url, imageDownloadPath, progress)
            .ConfigureAwait(false);

        return imageDownloadPath;
    }
}
