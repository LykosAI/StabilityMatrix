using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using Injectio.Attributes;
using Python.Runtime;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<IModelImportService, ModelImportService>]
public class ModelImportService(
    IDownloadService downloadService,
    INotificationService notificationService,
    ITrackedDownloadService trackedDownloadService
) : IModelImportService
{
    public static async Task<FilePath> SaveCmInfo(
        CivitModel model,
        CivitModelVersion modelVersion,
        CivitFile modelFile,
        DirectoryPath downloadDirectory
    )
    {
        var modelFileName = Path.GetFileNameWithoutExtension(modelFile.Name);
        var modelInfo = new ConnectedModelInfo(model, modelVersion, modelFile, DateTime.UtcNow);

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

    public async Task DoImport(
        CivitModel model,
        DirectoryPath downloadFolder,
        CivitModelVersion? selectedVersion = null,
        CivitFile? selectedFile = null,
        IProgress<ProgressReport>? progress = null,
        Func<Task>? onImportComplete = null,
        Func<Task>? onImportCanceled = null,
        Func<Task>? onImportFailed = null
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
            return;
        }

        // Get latest version file
        var modelFile =
            selectedFile ?? modelVersion.Files?.FirstOrDefault(x => x.Type == CivitFileType.Model);
        if (modelFile is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no files available",
                    "This model has no files available for download",
                    NotificationType.Warning
                )
            );
            return;
        }

        // Folders might be missing if user didn't install any packages yet
        downloadFolder.Create();

        // Fix invalid chars in FileName
        modelFile.Name = Path.GetInvalidFileNameChars()
            .Aggregate(modelFile.Name, (current, c) => current.Replace(c, '_'));

        // New code: Ensure unique file name
        var originalFileName = modelFile.Name;
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
            modelFile.Name = uniqueFileName;
        }

        var downloadPath = downloadFolder.JoinFile(modelFile.Name);

        // Download model info and preview first
        var cmInfoPath = await SaveCmInfo(model, modelVersion, modelFile, downloadFolder);
        var previewImagePath = await SavePreviewImage(modelVersion, downloadPath);

        // Create tracked download
        var download = trackedDownloadService.NewDownload(modelFile.DownloadUrl, downloadPath);

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
        // Folders might be missing if user didn't install any packages yet
        downloadFolder.Create();

        // Fix invalid chars in FileName
        var modelBaseFileName = Path.GetFileNameWithoutExtension(modelFileName);
        modelBaseFileName = Path.GetInvalidFileNameChars()
            .Aggregate(modelBaseFileName, (current, c) => current.Replace(c, '_'));
        var modelFileExtension = Path.GetExtension(modelFileName);

        var downloadPath = downloadFolder.JoinFile(modelBaseFileName + modelFileExtension);

        // Save model info and preview image first if available
        var cleanupFilePaths = new List<string>();
        if (connectedModelInfo is not null)
        {
            await connectedModelInfo.SaveJsonToDirectory(downloadFolder, modelBaseFileName);
            cleanupFilePaths.Add(
                downloadFolder.JoinFile(modelBaseFileName + ConnectedModelInfo.FileExtension)
            );
        }
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

            var previewImageDownloadPath = downloadFolder.JoinFile(
                modelBaseFileName + ".preview" + previewImageFileExtension
            );

            await notificationService.TryAsync(
                downloadService.DownloadToFileAsync(previewImageUri.ToString(), previewImageDownloadPath),
                "Could not download preview image"
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
