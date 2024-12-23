using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            selectedFile?.Type == CivitFileType.VAE
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

        Dispatcher.UIThread.Post(
            () => notificationService.Show("Download Started", $"Downloading {selectedFile.Name}")
        );
    }
}
