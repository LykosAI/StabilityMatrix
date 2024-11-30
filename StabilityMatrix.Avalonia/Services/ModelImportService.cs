using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[Singleton(typeof(IModelImportService))]
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
}
