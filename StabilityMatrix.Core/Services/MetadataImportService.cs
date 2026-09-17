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
    /// <summary>
    /// Consecutive rate-limit/connectivity failures after which a bulk scan stops early:
    /// once CivitAI starts refusing, hammering the remaining queue only extends the ban.
    /// </summary>
    private const int MaxConsecutiveLookupFailures = 3;

    public async Task<MetadataScanResult> ScanDirectoryForMissingInfo(
        DirectoryPath directory,
        IProgress<ProgressReport>? progress = null
    )
    {
        progress?.Report(new ProgressReport(-1f, message: "Scanning directory...", isIndeterminate: true));

        var checkpointsWithoutMetadata = new List<(FilePath File, ConnectedModelInfo? Existing)>();
        foreach (
            var file in directory
                .EnumerateFiles("*", EnumerationOptionConstants.AllDirectories)
                .Where(f => LocalModelFile.SupportedCheckpointExtensions.Contains(f.Extension))
        )
        {
            var (eligible, existing) = await GetSearchEligibilityAsync(file).ConfigureAwait(false);
            if (eligible)
            {
                checkpointsWithoutMetadata.Add((file, existing));
            }
        }

        var scanned = 0;
        var success = 0;
        var notFound = 0;
        var failed = 0;
        var consecutiveLookupFailures = 0;
        var aborted = false;

        foreach (var (checkpointFilePath, existingCmInfo) in checkpointsWithoutMetadata)
        {
            progress?.Report(
                new ProgressReport(
                    current: scanned,
                    total: checkpointsWithoutMetadata.Count,
                    message: scanned == 0
                        ? "Scanning directory..."
                        : $"{success} of {scanned} files matched so far"
                )
            );

            var fileNameWithoutExtension = checkpointFilePath.NameWithoutExtension;
            var cmInfoPath = checkpointFilePath.Directory?.JoinFile(
                $"{fileNameWithoutExtension}.cm-info.json"
            );

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
                    logger.LogWarning("Blake3 hash was null for {Path}", checkpointFilePath);
                    scanned++;
                    failed++;
                    continue;
                }

                var lookup = await modelFinder.RemoteFindModelWithStatus(blake3).ConfigureAwait(false);

                if (
                    lookup.Status
                    is RemoteModelLookupStatus.RateLimited
                        or RemoteModelLookupStatus.Unavailable
                )
                {
                    scanned++;
                    failed++;
                    consecutiveLookupFailures++;

                    if (consecutiveLookupFailures >= MaxConsecutiveLookupFailures)
                    {
                        aborted = true;
                        progress?.Report(
                            new ProgressReport(
                                current: scanned,
                                total: checkpointsWithoutMetadata.Count,
                                message: "CivitAI is not responding (rate limited or offline) - stopping early"
                            )
                        );
                        break;
                    }

                    continue;
                }

                consecutiveLookupFailures = 0;

                if (lookup.Status == RemoteModelLookupStatus.NotFound)
                {
                    logger.LogInformation(
                        "No CivitAI match for {Path} (hash {Hash})",
                        checkpointFilePath,
                        blake3
                    );
                    scanned++;
                    notFound++;
                    continue;
                }

                var updatedCmInfo = await BuildConnectedModelInfoAsync(
                        checkpointFilePath,
                        existingCmInfo,
                        lookup.Result!.Value,
                        progress
                    )
                    .ConfigureAwait(false);

                await updatedCmInfo
                    .SaveJsonToDirectory(checkpointFilePath.Directory, fileNameWithoutExtension)
                    .ConfigureAwait(false);

                scanned++;
                success++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while scanning {checkpointFilePath}", checkpointFilePath);
                scanned++;
                failed++;
            }
        }

        progress?.Report(
            new ProgressReport(
                current: scanned,
                total: checkpointsWithoutMetadata.Count,
                message: $"Metadata found for {success}/{checkpointsWithoutMetadata.Count} files"
            )
        );

        return new MetadataScanResult
        {
            Total = checkpointsWithoutMetadata.Count,
            Found = success,
            NotFound = notFound,
            Failed = failed,
            Aborted = aborted,
        };
    }

    /// <summary>
    /// A model is eligible for a bulk metadata search unless it is already linked to a source
    /// or the user opted it out (Disconnect). A sidecar that only carries custom or user data
    /// (categories, an edited name) keeps the model eligible; its contents survive the match
    /// via <see cref="MergeRemoteMetadata"/>.
    /// </summary>
    private static async Task<(bool Eligible, ConnectedModelInfo? Existing)> GetSearchEligibilityAsync(
        FilePath modelFile
    )
    {
        var cmInfoPath = modelFile.Directory?.JoinFile(
            modelFile.NameWithoutExtension + ConnectedModelInfo.FileExtension
        );
        if (cmInfoPath is null || !File.Exists(cmInfoPath))
        {
            return (true, null);
        }

        ConnectedModelInfo? existing;
        try
        {
            existing = await ReadConnectedModelInfo(cmInfoPath).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Unreadable sidecar: treat as no metadata, the match will replace it
            return (true, null);
        }

        if (existing is null)
        {
            return (true, null);
        }

        var eligible = !existing.IsConnected && existing.User?.ExcludeFromMetadataSearch != true;
        return (eligible, existing);
    }

    public async Task<MetadataScanResult> UpdateExistingMetadata(
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
                cmInfo = await ReadConnectedModelInfo(cmInfoPath).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                cmInfo = null;
            }
            if (cmInfo == null)
                continue;

            // Disconnected by the user: leave it alone
            if (cmInfo.User?.ExcludeFromMetadataSearch == true)
                continue;

            cmInfoList.Add(cmInfoPath, cmInfo);
        }

        var processed = 0;
        var success = 0;
        var notFound = 0;
        var failed = 0;
        var skipped = 0;
        var consecutiveLookupFailures = 0;
        var aborted = false;

        foreach (var (filePath, existingCmInfo) in cmInfoList)
        {
            processed++;
            progress?.Report(
                new ProgressReport(
                    current: processed,
                    total: cmInfoList.Count,
                    message: $"Updating metadata {processed}/{cmInfoList.Count}"
                )
            );

            try
            {
                var hash = existingCmInfo.Hashes?.BLAKE3;
                if (string.IsNullOrWhiteSpace(hash))
                {
                    // Imports from sources without BLAKE3 (HuggingFace, OpenModelDb) can still be
                    // refreshed by hashing the model file itself
                    hash = await ComputeHashForCmInfo(filePath, progress).ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(hash))
                {
                    logger.LogWarning(
                        "Skipping {Path}: no BLAKE3 hash stored and no model file found to hash",
                        filePath
                    );
                    skipped++;
                    continue;
                }

                var lookup = await modelFinder.RemoteFindModelWithStatus(hash).ConfigureAwait(false);

                if (
                    lookup.Status
                    is RemoteModelLookupStatus.RateLimited
                        or RemoteModelLookupStatus.Unavailable
                )
                {
                    failed++;
                    consecutiveLookupFailures++;

                    if (consecutiveLookupFailures >= MaxConsecutiveLookupFailures)
                    {
                        aborted = true;
                        progress?.Report(
                            new ProgressReport(
                                current: processed,
                                total: cmInfoList.Count,
                                message: "CivitAI is not responding (rate limited or offline) - stopping early"
                            )
                        );
                        break;
                    }

                    continue;
                }

                consecutiveLookupFailures = 0;

                if (lookup.Status == RemoteModelLookupStatus.NotFound)
                {
                    logger.LogInformation("No CivitAI match for {Path} (hash {Hash})", filePath, hash);
                    notFound++;
                    continue;
                }

                var updatedCmInfo = await BuildConnectedModelInfoAsync(
                        filePath,
                        existingCmInfo,
                        lookup.Result!.Value,
                        progress
                    )
                    .ConfigureAwait(false);

                var nameWithoutCmInfo = filePath.NameWithoutExtension.Replace(".cm-info", string.Empty);
                await updatedCmInfo
                    .SaveJsonToDirectory(filePath.Directory, nameWithoutCmInfo)
                    .ConfigureAwait(false);

                success++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while updating {filePath}", filePath);
                failed++;
            }
        }

        progress?.Report(
            new ProgressReport(
                current: processed,
                total: cmInfoList.Count,
                message: $"Metadata refreshed for {success}/{cmInfoList.Count} files"
            )
        );

        return new MetadataScanResult
        {
            Total = cmInfoList.Count,
            Found = success,
            NotFound = notFound,
            Failed = failed,
            Skipped = skipped,
            Aborted = aborted,
        };
    }

    /// <summary>
    /// Locates the model file belonging to a .cm-info.json and computes its BLAKE3 hash.
    /// Returns null when no sibling model file exists (e.g. the model was deleted).
    /// </summary>
    private static async Task<string?> ComputeHashForCmInfo(
        FilePath cmInfoPath,
        IProgress<ProgressReport>? progress
    )
    {
        var baseName = cmInfoPath.NameWithoutExtension.Replace(".cm-info", string.Empty);

        var modelFile = LocalModelFile
            .SupportedCheckpointExtensions.Select(ext => cmInfoPath.Directory?.JoinFile(baseName + ext))
            .FirstOrDefault(path => path is not null && File.Exists(path));

        if (modelFile is null)
            return null;

        var hashProgress = new Progress<ProgressReport>(report =>
        {
            progress?.Report(
                new ProgressReport(
                    current: report.Current ?? 0,
                    total: report.Total ?? 0,
                    message: $"Hashing {modelFile.Name} ... {report.Percentage}%",
                    printToConsole: false
                )
            );
        });

        return await FileHash.GetBlake3Async(modelFile, hashProgress).ConfigureAwait(false);
    }

    public async Task<MetadataFetchResult> GetMetadataForFile(
        FilePath filePath,
        IProgress<ProgressReport>? progress = null,
        bool forceReimport = false
    )
    {
        progress?.Report(new ProgressReport(-1f, message: "Getting metadata...", isIndeterminate: true));

        var fileNameWithoutExtension = filePath.NameWithoutExtension;
        var cmInfoPath = filePath.Directory?.JoinFile($"{fileNameWithoutExtension}.cm-info.json");
        var existingCmInfo =
            cmInfoPath is not null && File.Exists(cmInfoPath)
                ? await ReadConnectedModelInfo(cmInfoPath).ConfigureAwait(false)
                : null;
        // Custom or category-only metadata doesn't count as "already connected"
        if (existingCmInfo is { IsConnected: true } && !forceReimport)
            return new MetadataFetchResult(MetadataFetchStatus.AlreadyExists, null);

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
            logger.LogWarning("Blake3 hash was null for {Path}", filePath);
            return new MetadataFetchResult(MetadataFetchStatus.HashUnavailable, null);
        }

        var lookup = await modelFinder.RemoteFindModelWithStatus(blake3).ConfigureAwait(false);

        if (lookup.Status != RemoteModelLookupStatus.Found)
        {
            logger.LogInformation("Metadata lookup for {Path} returned {Status}", filePath, lookup.Status);

            return new MetadataFetchResult(
                lookup.Status switch
                {
                    RemoteModelLookupStatus.NotFound => MetadataFetchStatus.NotFound,
                    RemoteModelLookupStatus.RateLimited => MetadataFetchStatus.RateLimited,
                    _ => MetadataFetchStatus.Unavailable,
                },
                null
            );
        }

        var updatedCmInfo = await BuildConnectedModelInfoAsync(
                filePath,
                existingCmInfo,
                lookup.Result!.Value,
                progress
            )
            .ConfigureAwait(false);
        await updatedCmInfo
            .SaveJsonToDirectory(filePath.Directory, fileNameWithoutExtension)
            .ConfigureAwait(false);

        return new MetadataFetchResult(MetadataFetchStatus.Found, updatedCmInfo);
    }

    private async Task<ConnectedModelInfo> BuildConnectedModelInfoAsync(
        FilePath modelFilePath,
        ConnectedModelInfo? existingCmInfo,
        ModelSearchResult modelInfo,
        IProgress<ProgressReport>? progress
    )
    {
        var (model, modelVersion, modelFile) = modelInfo;
        var updatedCmInfo = MergeRemoteMetadata(
            existingCmInfo,
            new ConnectedModelInfo(model, modelVersion, modelFile, DateTimeOffset.UtcNow)
        );

        if (!string.IsNullOrWhiteSpace(updatedCmInfo.ThumbnailImageUrl))
            return updatedCmInfo;

        var image = modelVersion.Images?.FirstOrDefault(img =>
            LocalModelFile.SupportedImageExtensions.Contains(Path.GetExtension(img.Url))
            && img.Type == "image"
        );

        if (image != null)
        {
            updatedCmInfo.ThumbnailImageUrl = await DownloadImage(image, modelFilePath, progress)
                .ConfigureAwait(false);
        }

        return updatedCmInfo;
    }

    private static ConnectedModelInfo MergeRemoteMetadata(
        ConnectedModelInfo? existingCmInfo,
        ConnectedModelInfo refreshedCmInfo
    )
    {
        if (existingCmInfo == null)
            return refreshedCmInfo;

        refreshedCmInfo.ImportedAt =
            existingCmInfo.ImportedAt == default ? refreshedCmInfo.ImportedAt : existingCmInfo.ImportedAt;
        refreshedCmInfo.UserTitle = existingCmInfo.UserTitle;
        refreshedCmInfo.ThumbnailImageUrl = existingCmInfo.ThumbnailImageUrl;
        refreshedCmInfo.InferenceDefaults = existingCmInfo.InferenceDefaults;

        // The user block is carried across; an explicit per-model request also ends a Disconnect opt-out
        if (existingCmInfo.User is { } user)
        {
            refreshedCmInfo.User = user.Clone();
            refreshedCmInfo.User.ExcludeFromMetadataSearch = false;
        }

        // Display fields on an unlinked sidecar were typed by the user (Edit Metadata or a
        // Disconnect), so a match must not replace them.
        if (existingCmInfo.HasCustomMetadata)
        {
            refreshedCmInfo.ModelName = existingCmInfo.ModelName;
            refreshedCmInfo.ModelDescription = existingCmInfo.ModelDescription;
            refreshedCmInfo.Nsfw = existingCmInfo.Nsfw;
        }

        // Tags can be edited by the user and are indistinguishable from remote ones, so keep
        // every existing tag rather than replacing the list wholesale.
        if (existingCmInfo.Tags is { Length: > 0 } existingTags)
        {
            refreshedCmInfo.Tags = (refreshedCmInfo.Tags ?? [])
                .Union(existingTags, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return refreshedCmInfo;
    }

    private static async Task<ConnectedModelInfo?> ReadConnectedModelInfo(FilePath cmInfoPath)
    {
        return JsonSerializer.Deserialize<ConnectedModelInfo>(
            await cmInfoPath.ReadAllTextAsync().ConfigureAwait(false)
        );
    }

    private static async Task<string?> GetBlake3Hash(
        FilePath? cmInfoPath,
        FilePath checkpointFilePath,
        IProgress<ProgressReport> hashProgress
    )
    {
        if (!string.IsNullOrWhiteSpace(cmInfoPath?.ToString()) && File.Exists(cmInfoPath))
        {
            var cmInfo = await ReadConnectedModelInfo(cmInfoPath).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(cmInfo?.Hashes?.BLAKE3))
            {
                return cmInfo.Hashes.BLAKE3;
            }
        }

        // No stored hash (fresh file, or an import source that only records SHA256) - hash the file
        return await FileHash.GetBlake3Async(checkpointFilePath, hashProgress).ConfigureAwait(false);
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
