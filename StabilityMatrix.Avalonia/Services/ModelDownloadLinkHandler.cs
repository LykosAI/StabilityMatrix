using System.Web;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Injectio.Attributes;
using MessagePipe;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<IModelDownloadLinkHandler, ModelDownloadLinkHandler>]
public class ModelDownloadLinkHandler(
    IDistributedSubscriber<string, Uri> uriHandlerSubscriber,
    ILogger<ModelDownloadLinkHandler> logger,
    ICivitApi civitApi,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    IModelImportService modelImportService
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
        if (!receivedUri.Host.Equals(DownloadCivitModel, StringComparison.OrdinalIgnoreCase))
            return;

        logger.LogDebug("ModelDownloadLinkHandler Received URI: {Uri}", receivedUri.PathAndQuery);

        var queryDict = HttpUtility.ParseQueryString(receivedUri.Query);
        var modelIdStr = queryDict["modelId"];
        var modelVersionIdStr = queryDict["modelVersionId"];
        var fileIdStr = queryDict["fileId"];
        var type = queryDict["type"];
        var format = queryDict["format"];
        var size = queryDict["size"];
        var fp = queryDict["fp"];

        int? fileId = int.TryParse(fileIdStr, out var parsedFileId) ? parsedFileId : null;
        var hasFileId = fileId.HasValue;

        // Civitai's newer download URLs only expose modelVersionId (in the path) and fileId.
        // When we have a fileId we can resolve the file directly, so the legacy type/format
        // requirement is only enforced for old-style links that omit fileId.
        var hasValidLegacyFilter =
            !string.IsNullOrWhiteSpace(type)
            && !string.IsNullOrWhiteSpace(format)
            && Enum.TryParse<CivitFileType>(type, out _)
            && Enum.TryParse<CivitModelFormat>(format, out _);

        if (
            string.IsNullOrWhiteSpace(modelIdStr)
            || !int.TryParse(modelIdStr, out var modelId)
            || (!hasFileId && !hasValidLegacyFilter)
        )
        {
            logger.LogError("ModelDownloadLinkHandler: Invalid query parameters");

            Dispatcher.UIThread.Post(() =>
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

        Dispatcher.UIThread.Post(() =>
            notificationService.Show(
                "Link Received",
                "Successfully received download link",
                NotificationType.Warning
            )
        );

        var modelTask = civitApi.GetModelById(modelId);
        modelTask.Wait();
        var model = modelTask.Result;

        var useModelVersion = int.TryParse(modelVersionIdStr, out var modelVersionId);

        var modelVersion = useModelVersion
            ? model.ModelVersions?.FirstOrDefault(x => x.Id == modelVersionId)
            : model.ModelVersions?.FirstOrDefault();

        // If we have a fileId but the version lookup failed (or wasn't supplied), find the
        // version that actually owns the requested file.
        if (modelVersion is null && hasFileId)
        {
            modelVersion = model.ModelVersions?.FirstOrDefault(v =>
                v.Files?.Any(f => f.Id == fileId!.Value) == true
            );
        }

        if (modelVersion is null)
        {
            logger.LogError("ModelDownloadLinkHandler: Model version not found");
            Dispatcher.UIThread.Post(() =>
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

        CivitFile? selectedFile;

        if (hasFileId)
        {
            // Resolve directly by file id. Fall back to scanning other versions in case the
            // supplied modelVersionId doesn't line up with where the file actually lives.
            selectedFile =
                modelVersion.Files?.FirstOrDefault(x => x.Id == fileId!.Value)
                ?? model
                    .ModelVersions?.SelectMany(v => v.Files ?? Enumerable.Empty<CivitFile>())
                    .FirstOrDefault(f => f.Id == fileId!.Value);

            // Re-align modelVersion if the file actually belongs to a different version.
            if (selectedFile is not null && modelVersion.Files?.Any(f => f.Id == selectedFile.Id) != true)
            {
                modelVersion =
                    model.ModelVersions?.FirstOrDefault(v =>
                        v.Files?.Any(f => f.Id == selectedFile.Id) == true
                    ) ?? modelVersion;
            }
        }
        else
        {
            Enum.TryParse<CivitFileType>(type, out var civitFileType);
            Enum.TryParse<CivitModelFormat>(format, out var civitFormat);

            var possibleFiles = modelVersion.Files?.Where(x =>
                x.Type == civitFileType && x.Metadata.Format == civitFormat
            );

            if (!string.IsNullOrWhiteSpace(fp))
            {
                possibleFiles = possibleFiles?.Where(x =>
                    x.Metadata.Fp != null && x.Metadata.Fp.Equals(fp, StringComparison.OrdinalIgnoreCase)
                );
            }

            if (!string.IsNullOrWhiteSpace(size))
            {
                possibleFiles = possibleFiles?.Where(x => x.Metadata.Size != null && x.Metadata.Size == size);
            }

            possibleFiles = possibleFiles?.ToList();

            if (possibleFiles is null)
            {
                Dispatcher.UIThread.Post(() =>
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

            selectedFile = possibleFiles.FirstOrDefault() ?? modelVersion.Files?.FirstOrDefault();
        }

        if (selectedFile is null)
        {
            Dispatcher.UIThread.Post(() =>
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

        var rootModelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);
        var downloadDirectory = rootModelsDirectory.JoinDir(
            selectedFile.Type == CivitFileType.VAE
                ? SharedFolderType.VAE.GetStringValue()
                : model.Type.ConvertTo<SharedFolderType>().GetStringValue()
        );

        var importTask = modelImportService.DoImport(
            model,
            downloadDirectory,
            selectedVersion: modelVersion,
            selectedFile: selectedFile
        );
        importTask.Wait();

        Dispatcher.UIThread.Post(() =>
            notificationService.Show("Download Started", $"Downloading {selectedFile.Name}")
        );
    }
}
