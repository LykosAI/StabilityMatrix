using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Helper for uploading images to ComfyUI for BananaVision providers
/// </summary>
public static class ComfyImageUploadHelper
{
    private static async Task UploadAsPngAsync(
        IInferenceClientManager clientManager,
        Stream sourceStream,
        string fileName,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var client =
            clientManager.Client ?? throw new InvalidOperationException("Comfy client is not connected");

        try
        {
            sourceStream.Position = 0;
        }
        catch (NotSupportedException)
        {
            // Stream is not seekable - continue with current position
        }

        try
        {
            using var bitmap = new Bitmap(sourceStream);
            await using var pngStream = new MemoryStream();
            bitmap.Save(pngStream);
            pngStream.Position = 0;

            await client.UploadImageAsync(pngStream, fileName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to re-encode image to PNG, uploading original stream for {FileName}",
                fileName
            );
            try
            {
                sourceStream.Position = 0;
            }
            catch (NotSupportedException)
            {
                // Stream is not seekable - continue with current position
            }

            await client.UploadImageAsync(sourceStream, fileName, cancellationToken);
        }
    }

    /// <summary>
    /// Uploads input images and conversation history image to ComfyUI
    /// </summary>
    /// <param name="clientManager">Inference client manager</param>
    /// <param name="request">Generation request</param>
    /// <param name="maxInputImages">Maximum number of input images supported by the provider</param>
    /// <param name="providerPrefix">Prefix for uploaded filenames (e.g. "flux_kontext")</param>
    /// <param name="logger">Logger</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task UploadImagesAsync(
        IInferenceClientManager clientManager,
        ImageGenerationRequest request,
        int maxInputImages,
        string providerPrefix,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        // Upload input images if provided
        if (request.InputImages is { Count: > 0 })
        {
            logger.LogInformation("Uploading {Count} input images", request.InputImages.Count);

            for (var i = 0; i < Math.Min(request.InputImages.Count, maxInputImages); i++)
            {
                var image = request.InputImages[i];
                var inputImageBytes = Convert.FromBase64String(image.Base64Data);
                using var inputStream = new MemoryStream(inputImageBytes);
                var fileName = $"{providerPrefix}_input_{i}.png";

                await UploadAsPngAsync(clientManager, inputStream, fileName, logger, cancellationToken);
            }
        }

        // Upload most recent conversation history image for reference
        // (if not already providing max input images, as we need a slot for history)
        if ((request.InputImages?.Count ?? 0) < maxInputImages && request.ConversationHistory != null)
        {
            // Find the last assistant message with an image
            var lastAssistantImage = request.ConversationHistory.LastOrDefault(m =>
                m is { Role: MessageRole.Assistant, ImageContent: not null }
            );

            if (lastAssistantImage?.ImageContent != null)
            {
                var fileName = $"{providerPrefix}_history_latest.png";
                var imageContent = lastAssistantImage.ImageContent;

                // Prefer uploading directly from file path if available (more efficient)
                if (!string.IsNullOrEmpty(imageContent.FilePath) && File.Exists(imageContent.FilePath))
                {
                    logger.LogInformation(
                        "Uploading conversation history image from file: {FilePath}",
                        imageContent.FilePath
                    );

                    await using var fileStream = File.OpenRead(imageContent.FilePath);
                    await UploadAsPngAsync(clientManager, fileStream, fileName, logger, cancellationToken);

                    logger.LogInformation("Successfully uploaded history image: {FileName}", fileName);
                }
                else
                {
                    // Fallback to base64 data
                    logger.LogInformation("Uploading conversation history image from base64 data");

                    var historyImageBytes = Convert.FromBase64String(imageContent.Base64Data);
                    using var historyStream = new MemoryStream(historyImageBytes);
                    await UploadAsPngAsync(clientManager, historyStream, fileName, logger, cancellationToken);

                    logger.LogInformation(
                        "Successfully uploaded history image: {FileName} (Size: {Size} bytes)",
                        fileName,
                        historyImageBytes.Length
                    );
                }
            }
            else
            {
                logger.LogDebug(
                    "No conversation history image found to upload (InputImages count: {Count})",
                    request.InputImages?.Count ?? 0
                );
            }
        }
    }
}
