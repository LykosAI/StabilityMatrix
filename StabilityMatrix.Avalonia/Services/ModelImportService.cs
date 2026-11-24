using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<IModelImportService, ModelImportService>]
public class ModelImportService(
    IDownloadService downloadService,
    INotificationService notificationService,
    ITrackedDownloadService trackedDownloadService,
    IModelIndexService modelIndexService,
    ISettingsManager settingsManager
) : IModelImportService
{
    public static async Task<FilePath> SaveCmInfo(
        CivitModel model,
        CivitModelVersion modelVersion,
        CivitFile modelFile,
        DirectoryPath downloadDirectory,
        string? fileNameOverride = null,
        SamplerCardViewModel? samplerCardVm = null
    )
    {
        var modelFileName = fileNameOverride ?? Path.GetFileNameWithoutExtension(modelFile.Name);
        InferenceDefaults? inferenceDefaults = null;
        if (samplerCardVm != null)
        {
            inferenceDefaults = new InferenceDefaults
            {
                Sampler = samplerCardVm.SelectedSampler,
                Scheduler = samplerCardVm.SelectedScheduler,
                CfgScale = samplerCardVm.CfgScale,
                Steps = samplerCardVm.Steps,
                Width = samplerCardVm.Width,
                Height = samplerCardVm.Height,
            };
        }

        var modelInfo = new ConnectedModelInfo(
            model,
            modelVersion,
            modelFile,
            DateTime.UtcNow,
            inferenceDefaults
        );

        await modelInfo.SaveJsonToDirectory(downloadDirectory, modelFileName);

        var jsonName = $"{modelFileName}.cm-info.json";
        return downloadDirectory.JoinFile(jsonName);
    }

    /// <summary>
    /// Saves the preview image to the same directory as the model file
    /// </summary>
    /// <param name="modelVersion"></param>
    /// <param name="modelFilePath"></param>
    /// <returns>The file path of the saved preview image</returns>
    public async Task<FilePath?> SavePreviewImage(CivitModelVersion modelVersion, FilePath modelFilePath)
    {
        // Skip if model has no images
        if (modelVersion.Images == null || modelVersion.Images.Count == 0)
        {
            return null;
        }

        var image = modelVersion.Images.FirstOrDefault(x => x.Type == "image");
        if (image is null)
            return null;

        var imageExtension = Path.GetExtension(image.Url).TrimStart('.');
        if (imageExtension is "jpg" or "jpeg" or "png")
        {
            var imageDownloadPath = modelFilePath.Directory!.JoinFile(
                $"{modelFilePath.NameWithoutExtension}.preview.{imageExtension}"
            );

            var imageTask = downloadService.DownloadToFileAsync(image.Url, imageDownloadPath);
            await notificationService.TryAsync(imageTask, "Could not download preview image");

            return imageDownloadPath;
        }

        return null;
    }

    public async Task<bool> DoImport(
        CivitModel model,
        DirectoryPath downloadFolder,
        CivitModelVersion? selectedVersion = null,
        CivitFile? selectedFile = null,
        string? fileNameOverride = null,
        SamplerCardViewModel? inferenceDefaults = null,
        IProgress<ProgressReport>? progress = null,
        Func<Task>? onImportComplete = null,
        Func<Task>? onImportCanceled = null,
        Func<Task>? onImportFailed = null,
        DuplicateDownloadHandling duplicateHandling = DuplicateDownloadHandling.Prompt
    )
    {
        // Get latest version
        var modelVersion = selectedVersion ?? model.ModelVersions?.FirstOrDefault();
        if (modelVersion is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            return false;
        }

        // Get latest version file
        var modelFile = selectedFile ?? modelVersion.Files?.FirstOrDefault(x => x.Type.IsModelWeights());
        if (modelFile is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no files available",
                    "This model has no files available for download",
                    NotificationType.Warning
                )
            );
            return false;
        }

        // Guard against downloading a file whose hash is already in the library
        if (
            duplicateHandling is not DuplicateDownloadHandling.Allow
            && await FindInstalledDuplicateAsync(modelFile.Hashes.BLAKE3, modelFile.Hashes.SHA256)
                is { } duplicate
        )
        {
            if (duplicateHandling is DuplicateDownloadHandling.Prompt)
            {
                var downloadAnyway = await Dispatcher.UIThread.InvokeAsync(() =>
                    ShowDuplicateDownloadDialogAsync(modelFile.Name, duplicate.RelativePath)
                );

                if (!downloadAnyway)
                {
                    // Cancel path so callers reset their in-progress UI state
                    onImportCanceled?.Invoke().SafeFireAndForget();
                    return false;
                }
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    notificationService.Show(
                        new Notification(
                            "Already in library",
                            $"\"{modelFile.Name}\" matches an installed file: \"{duplicate.RelativePath}\". Download skipped.",
                            NotificationType.Information
                        )
                    );
                });

                onImportCanceled?.Invoke().SafeFireAndForget();
                return false;
            }
        }

        if (fileNameOverride != null && (fileNameOverride.Contains("\\") || fileNameOverride.Contains("/")))
        {
            // figure out the folder path to add to downloadFolder
            var lastIndex = fileNameOverride.LastIndexOfAny(['\\', '/']);
            if (lastIndex >= 0)
            {
                // Extract folder path and file name
                var folderPath = fileNameOverride.Substring(0, lastIndex);
                fileNameOverride = fileNameOverride.Substring(lastIndex + 1);

                // Join with download folder
                downloadFolder = downloadFolder.JoinDir(folderPath);
            }
        }

        // Folders might be missing if user didn't install any packages yet
        downloadFolder.Create();

        var originalFileName =
            fileNameOverride == null
                ? modelFile.Name
                : $@"{fileNameOverride}{Path.GetExtension(modelFile.Name)}";

        // Fix invalid chars in FileName
        originalFileName = Path.GetInvalidFileNameChars()
            .Aggregate(originalFileName, (current, c) => current.Replace(c, '_'));

        // Generate unique file name if it already exists
        var uniqueFileName = GenerateUniqueFileName(downloadFolder.ToString(), originalFileName);
        if (!uniqueFileName.Equals(originalFileName, StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(() =>
            {
                notificationService.Show(
                    new Notification(
                        "File renamed",
                        $"A file with the name \"{originalFileName}\" already exists. The model will be saved as \"{uniqueFileName}\"."
                    )
                );
            });
        }

        var downloadPath = downloadFolder.JoinFile(uniqueFileName);

        // A stale paused/pending download for the same target (e.g. loaded from a previous
        // session) shares this download's sidecar file names — cancelling it later would
        // delete them out from under us. Supersede it now: its temp file is removed, the
        // sidecars stay with this download.
        var staleDownloads = trackedDownloadService
            .Downloads.Where(existing =>
                existing.ProgressState
                    is ProgressState.Inactive
                        or ProgressState.Paused
                        or ProgressState.Pending
                && string.Equals(existing.FileName, uniqueFileName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    existing.DownloadDirectory.ToString(),
                    downloadFolder.ToString(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        foreach (var staleDownload in staleDownloads)
        {
            staleDownload.Supersede();
        }

        // Download model info and preview first
        var cmInfoPath = await SaveCmInfo(
            model,
            modelVersion,
            modelFile,
            downloadFolder,
            Path.GetFileNameWithoutExtension(uniqueFileName),
            inferenceDefaults
        );
        var previewImagePath = await SavePreviewImage(modelVersion, downloadPath);

        // Create tracked download
        var download = trackedDownloadService.NewDownload(
            modelFile.GetFileSpecificDownloadUrl(),
            downloadPath
        );

        // Add hash info
        download.ExpectedHashSha256 = modelFile.Hashes.SHA256;

        // Add files to cleanup list
        download.ExtraCleanupFileNames.Add(cmInfoPath);
        if (previewImagePath is not null)
        {
            download.ExtraCleanupFileNames.Add(previewImagePath);
        }

        // Attach for progress updates
        download.ProgressUpdate += (s, e) =>
        {
            progress?.Report(e);
        };

        download.ProgressStateChanged += (s, e) =>
        {
            if (e == ProgressState.Success)
            {
                onImportComplete?.Invoke().SafeFireAndForget();
            }
            else if (e == ProgressState.Cancelled)
            {
                onImportCanceled?.Invoke().SafeFireAndForget();
            }
            else if (e == ProgressState.Failed)
            {
                onImportFailed?.Invoke().SafeFireAndForget();
            }
        };

        // Add hash context action
        download.ContextAction = CivitPostDownloadContextAction.FromCivitFile(modelFile);

        await trackedDownloadService.TryStartDownload(download);

        return true;
    }

    /// <summary>
    /// Asks whether to download a file that matches an already-installed model.
    /// Returns true to download anyway; defaults to skipping.
    /// </summary>
    private static async Task<bool> ShowDuplicateDownloadDialogAsync(
        string fileName,
        string existingRelativePath
    )
    {
        var dialog = DialogHelper.CreateTaskDialog(
            "Already in library",
            $"\"{fileName}\" matches an installed file: \"{existingRelativePath}\"."
        );

        dialog.ShowProgressBar = false;
        dialog.Buttons = new List<TaskDialogButton>
        {
            new("Download Anyway", TaskDialogStandardResult.Yes),
            new("Skip", TaskDialogStandardResult.Cancel) { IsDefault = true },
        };

        return (TaskDialogStandardResult)await dialog.ShowAsync(true) == TaskDialogStandardResult.Yes;
    }

    /// <summary>
    /// Finds an installed model matching either of the given hashes, verifying the indexed
    /// file still exists on disk so a stale index entry can't block a legitimate re-download.
    /// </summary>
    private async Task<LocalModelFile?> FindInstalledDuplicateAsync(string? hashBlake3, string? hashSha256)
    {
        var candidates = new List<LocalModelFile>();

        if (!string.IsNullOrEmpty(hashBlake3))
        {
            candidates.AddRange(await modelIndexService.FindByHashAsync(hashBlake3));
        }

        if (candidates.Count == 0 && !string.IsNullOrEmpty(hashSha256))
        {
            candidates.AddRange(await modelIndexService.FindBySha256Async(hashSha256));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var existing = candidates.FirstOrDefault(candidate =>
            File.Exists(candidate.GetFullPath(settingsManager.ModelsDirectory))
        );

        if (existing is null)
        {
            // Indexed files were removed outside the app; refresh so the index catches up
            modelIndexService.BackgroundRefreshIndex();
        }

        return existing;
    }

    public Task DoOpenModelDbImport(
        OpenModelDbKeyedModel model,
        OpenModelDbResource resource,
        DirectoryPath downloadFolder,
        Action<TrackedDownload>? configureDownload = null
    )
    {
        // todo: maybe can get actual filename from url?
        ArgumentException.ThrowIfNullOrEmpty(model.Id, nameof(model));
        ArgumentException.ThrowIfNullOrEmpty(resource.Type, nameof(resource));
        var modelFileName = $"{model.Id}.{resource.Type}";

        var modelUris = resource.Urls?.Select(u => new Uri(u, UriKind.Absolute)).ToArray();
        if (modelUris is null || modelUris.Length == 0)
        {
            notificationService.Show(
                new Notification(
                    "Model has no download links",
                    "This model has no download links available",
                    NotificationType.Warning
                )
            );
            return Task.CompletedTask;
        }

        return DoCustomImport(
            modelUris,
            modelFileName,
            downloadFolder,
            model.Images?.SelectImageAbsoluteUris().FirstOrDefault(),
            configureDownload: configureDownload,
            connectedModelInfo: new ConnectedModelInfo(model, resource, DateTimeOffset.Now)
        );
    }

    public async Task DoCustomImport(
        IEnumerable<Uri> modelUris,
        string modelFileName,
        DirectoryPath downloadFolder,
        Uri? previewImageUri = null,
        string? previewImageFileExtension = null,
        ConnectedModelInfo? connectedModelInfo = null,
        Action<TrackedDownload>? configureDownload = null
    )
    {
        // Subfolder support for user-defined patterns such as
        // "{base_model}/{model_name}/{file_name}". Treat every component as relative so
        // rooted or traversal input cannot escape the selected models folder.
        var pathSegments = modelFileName
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => segment is not "." and not "..")
            .Select(SanitizePathSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (pathSegments.Length == 0)
        {
            throw new ArgumentException(
                "Model file name must contain a valid file name.",
                nameof(modelFileName)
            );
        }

        modelFileName = pathSegments[^1];
        if (pathSegments.Length > 1)
        {
            downloadFolder = new DirectoryPath([
                downloadFolder.FullPath,
                .. pathSegments.Take(pathSegments.Length - 1),
            ]);
        }

        // Folders might be missing if user didn't install any packages yet
        downloadFolder.Create();

        // Fix invalid chars in FileName
        var modelBaseFileName = SanitizePathSegment(Path.GetFileNameWithoutExtension(modelFileName));
        var modelFileExtension = Path.GetExtension(modelFileName);

        var downloadPath = downloadFolder.JoinFile(modelBaseFileName + modelFileExtension);

        // Save model info first if available. Preview image downloads can be slow
        // or hosted on flaky third-party mirrors, so start the model download before
        // fetching the preview in the background.
        var cleanupFilePaths = new List<string>();
        if (connectedModelInfo is not null)
        {
            await connectedModelInfo.SaveJsonToDirectory(downloadFolder, modelBaseFileName);
            cleanupFilePaths.Add(
                downloadFolder.JoinFile(modelBaseFileName + ConnectedModelInfo.FileExtension)
            );
        }

        FilePath? previewImageDownloadPath = null;
        if (previewImageUri is not null)
        {
            if (previewImageFileExtension is null)
            {
                previewImageFileExtension = Path.GetExtension(previewImageUri.LocalPath);
                if (string.IsNullOrEmpty(previewImageFileExtension))
                {
                    throw new InvalidOperationException(
                        "Unable to get preview image file extension from from Uri, and no file extension provided"
                    );
                }
            }

            previewImageDownloadPath = downloadFolder.JoinFile(
                modelBaseFileName + ".preview" + previewImageFileExtension
            );

            cleanupFilePaths.Add(previewImageDownloadPath);
        }

        // Create tracked download
        // todo: support multiple uris
        var modelUri = modelUris.First();
        var download = trackedDownloadService.NewDownload(modelUri, downloadPath);

        // Add hash info
        // download.ExpectedHashSha256 = modelFile.Hashes.SHA256;

        // Add files to cleanup list
        download.ExtraCleanupFileNames.AddRange(cleanupFilePaths);

        // Configure
        configureDownload?.Invoke(download);

        // Add hash context action
        // download.ContextAction = CivitPostDownloadContextAction.FromCivitFile(modelFile);

        await trackedDownloadService.TryStartDownload(download);

        if (previewImageUri is not null && previewImageDownloadPath is not null)
        {
            DownloadPreviewImageAsync(previewImageUri, previewImageDownloadPath).SafeFireAndForget();
        }
    }

    private static string SanitizePathSegment(string segment) =>
        Path.GetInvalidFileNameChars().Aggregate(segment, (current, c) => current.Replace(c, '_'));

    private async Task DownloadPreviewImageAsync(Uri previewImageUri, FilePath previewImageDownloadPath)
    {
        await notificationService.TryAsync(
            downloadService.DownloadToFileAsync(previewImageUri.ToString(), previewImageDownloadPath),
            "Could not download preview image"
        );
    }

    private string GenerateUniqueFileName(string folder, string fileName)
    {
        var fullPath = Path.Combine(folder, fileName);
        if (!File.Exists(fullPath))
            return fileName;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var count = 1;
        string newFileName;

        do
        {
            newFileName = $"{name} ({count}){extension}";
            fullPath = Path.Combine(folder, newFileName);
            count++;
        } while (File.Exists(fullPath));

        return newFileName;
    }
}
