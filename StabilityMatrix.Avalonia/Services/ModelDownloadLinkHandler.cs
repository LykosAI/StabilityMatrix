using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using MessagePipe;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[Singleton(typeof(IModelDownloadLinkHandler))]
public class ModelDownloadLinkHandler(
    IDistributedSubscriber<string, Uri> uriHandlerSubscriber,
    ILogger<ModelDownloadLinkHandler> logger,
    ICivitApi civitApi,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    ITrackedDownloadService trackedDownloadService
) : IAsyncDisposable, IModelDownloadLinkHandler
{
    private IAsyncDisposable? uriHandlerSubscription;
    private const string DownloadCivitModel = "downloadCivitModel";

    public async Task StartListening()
    {
        uriHandlerSubscription = await uriHandlerSubscriber.SubscribeAsync(
            UriHandler.IpcKeySend,
            UriReceivedHandler
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (uriHandlerSubscription is not null)
        {
            await uriHandlerSubscription.DisposeAsync();
            uriHandlerSubscription = null;
        }
    }

    private void UriReceivedHandler(Uri receivedUri)
    {
        logger.LogDebug("ModelDownloadLinkHandler Received URI: {Uri}", receivedUri.PathAndQuery);
        if (!receivedUri.Host.Equals(DownloadCivitModel, StringComparison.OrdinalIgnoreCase))
            return;

        var queryDict = HttpUtility.ParseQueryString(receivedUri.Query);
        var modelIdStr = queryDict["modelId"];
        var modelVersionIdStr = queryDict["modelVersionId"];
        var type = queryDict["type"];
        var format = queryDict["format"];
        var size = queryDict["size"];
        var fp = queryDict["fp"];

        if (
            string.IsNullOrWhiteSpace(modelIdStr)
            || string.IsNullOrWhiteSpace(type)
            || string.IsNullOrWhiteSpace(format)
            || !int.TryParse(modelIdStr, out var modelId)
            || !Enum.TryParse<CivitFileType>(type, out var civitFileType)
            || !Enum.TryParse<CivitModelFormat>(format, out var civitFormat)
        )
        {
            logger.LogError("ModelDownloadLinkHandler: Invalid query parameters");

            Dispatcher.UIThread.Post(
                () =>
                    notificationService.Show(
                        new Notification(
                            "Invalid Download Link",
                            "The download link is invalid",
                            NotificationType.Error
                        )
                    )
            );
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
                notificationService.Show(
                    "Link Received",
                    "Successfully received download link",
                    NotificationType.Warning
                )
        );

        var modelTask = civitApi.GetModelById(modelId);
        modelTask.Wait();
        var model = modelTask.Result;

        var useModelVersion = !string.IsNullOrWhiteSpace(modelVersionIdStr);
        var modelVersionId = useModelVersion ? int.Parse(modelVersionIdStr) : 0;

        var modelVersion = useModelVersion
            ? model.ModelVersions?.FirstOrDefault(x => x.Id == modelVersionId)
            : model.ModelVersions?.FirstOrDefault();

        if (modelVersion is null)
        {
            logger.LogError("ModelDownloadLinkHandler: Model version not found");
            Dispatcher.UIThread.Post(
                () =>
                    notificationService.Show(
                        new Notification(
                            "Model has no versions available",
                            "This model has no versions available for download",
                            NotificationType.Error
                        )
                    )
            );
            return;
        }

        var possibleFiles = modelVersion.Files?.Where(
            x => x.Type == civitFileType && x.Metadata.Format == civitFormat
        );

        if (!string.IsNullOrWhiteSpace(fp))
        {
            possibleFiles = possibleFiles?.Where(
                x => x.Metadata.Fp != null && x.Metadata.Fp.Equals(fp, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (!string.IsNullOrWhiteSpace(size))
        {
            possibleFiles = possibleFiles?.Where(x => x.Metadata.Size != null && x.Metadata.Size == size);
        }

        possibleFiles = possibleFiles?.ToList();

        if (possibleFiles is null)
        {
            Dispatcher.UIThread.Post(
                () =>
                    notificationService.Show(
                        new Notification(
                            "Model has no files available",
                            "This model has no files available for download",
                            NotificationType.Error
                        )
                    )
            );
            logger.LogError("ModelDownloadLinkHandler: Model file not found");
            return;
        }

        var selectedFile = possibleFiles.FirstOrDefault() ?? modelVersion.Files?.FirstOrDefault();

        var rootModelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);
        var downloadDirectory = rootModelsDirectory.JoinDir(
            selectedFile.Type == CivitFileType.VAE
                ? SharedFolderType.VAE.GetStringValue()
                : model.Type.ConvertTo<SharedFolderType>().GetStringValue()
        );

        downloadDirectory.Create();
        var downloadPath = downloadDirectory.JoinFile(selectedFile.Name);

        // Create tracked download
        var download = trackedDownloadService.NewDownload(selectedFile.DownloadUrl, downloadPath);

        // Download model info and preview first
        var saveCmInfoTask = SaveCmInfo(model, modelVersion, selectedFile, downloadDirectory);
        var savePreviewImageTask = SavePreviewImage(modelVersion, downloadPath);

        Task.WaitAll([saveCmInfoTask, savePreviewImageTask]);

        var cmInfoPath = saveCmInfoTask.Result;
        var previewImagePath = savePreviewImageTask.Result;

        // Add hash info
        download.ExpectedHashSha256 = selectedFile.Hashes.SHA256;

        // Add files to cleanup list
        download.ExtraCleanupFileNames.Add(cmInfoPath);
        if (previewImagePath is not null)
        {
            download.ExtraCleanupFileNames.Add(previewImagePath);
        }

        // Add hash context action
        download.ContextAction = CivitPostDownloadContextAction.FromCivitFile(selectedFile);

        download.Start();

        Dispatcher.UIThread.Post(
            () => notificationService.Show("Download Started", $"Downloading {selectedFile.Name}")
        );
    }

    private static async Task<FilePath> SaveCmInfo(
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
    private async Task<FilePath?> SavePreviewImage(CivitModelVersion modelVersion, FilePath modelFilePath)
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
}
