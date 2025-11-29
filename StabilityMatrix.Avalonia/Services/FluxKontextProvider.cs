using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Flux Kontext using local ComfyUI backend
/// </summary>
public class FluxKontextProvider(ILogger<FluxKontextProvider> logger, IInferenceClientManager clientManager)
    : IImageGenerationProvider
{
    public string ProviderId => "flux-kontext";
    public string ProviderName => "Flux Kontext (Local)";
    public bool SupportsImageInput => true;
    public bool SupportsMultiTurn => true;

    public async Task<ImageGenerationResponse> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Check if ComfyUI is connected
            if (!clientManager.IsConnected)
            {
                logger.LogWarning("ComfyUI is not connected");
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage =
                        "ComfyUI is not connected. Please start a ComfyUI package and connect in the Inference tab.",
                };
            }

            // Validate models are available
            var modelManager = new FluxKontextModelManager();
            if (!modelManager.AreModelsAvailable(clientManager))
            {
                var modelsList = string.Join(", ", modelManager.GetMissingModelNames(clientManager));

                logger.LogWarning("Required models not found: {Models}", modelsList);
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage =
                        $"Required models not found: {modelsList}. Please download them from the HuggingFace model browser.",
                };
            }

            // Upload input images if provided
            if (request.InputImages is { Count: > 0 })
            {
                logger.LogInformation("Uploading {Count} input images", request.InputImages.Count);

                for (var i = 0; i < Math.Min(request.InputImages.Count, 2); i++) // Max 2 images
                {
                    var image = request.InputImages[i];
                    var inputImageBytes = Convert.FromBase64String(image.Base64Data);
                    using var inputStream = new MemoryStream(inputImageBytes);
                    var fileName = $"flux_kontext_input_{i}.png";

                    await clientManager.Client.UploadImageAsync(inputStream, fileName, cancellationToken);
                }
            }

            // Upload most recent conversation history image for reference (if not already providing 2 input images)
            if ((request.InputImages?.Count ?? 0) < 2 && request.ConversationHistory != null)
            {
                // Find the last assistant message with an image
                var lastAssistantImage = request.ConversationHistory.LastOrDefault(m =>
                    m is { Role: MessageRole.Assistant, ImageContent: not null }
                );

                if (lastAssistantImage?.ImageContent != null)
                {
                    const string fileName = "flux_kontext_history_latest.png";

                    // Prefer uploading directly from file path if available (more efficient)
                    if (
                        !string.IsNullOrEmpty(lastAssistantImage.ImageContent.FilePath)
                        && File.Exists(lastAssistantImage.ImageContent.FilePath)
                    )
                    {
                        logger.LogInformation(
                            "Uploading conversation history image from file: {FilePath}",
                            lastAssistantImage.ImageContent.FilePath
                        );

                        await using var fileStream = File.OpenRead(lastAssistantImage.ImageContent.FilePath);
                        await clientManager.Client.UploadImageAsync(fileStream, fileName, cancellationToken);

                        logger.LogInformation("Successfully uploaded history image: {FileName}", fileName);
                    }
                    else
                    {
                        // Fallback to base64 data
                        logger.LogInformation("Uploading conversation history image from base64 data");

                        var historyImageBytes = Convert.FromBase64String(
                            lastAssistantImage.ImageContent.Base64Data
                        );
                        using var historyStream = new MemoryStream(historyImageBytes);
                        await clientManager.Client.UploadImageAsync(
                            historyStream,
                            fileName,
                            cancellationToken
                        );

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

            // Extract custom model, LoRA selections, and resolution from provider options
            HybridModelFile? customUnetModel = null;
            IEnumerable<SelectedLora>? loras = null;
            int? width = null;
            int? height = null;

            if (request.ProviderOptions != null)
            {
                if (
                    request.ProviderOptions.TryGetValue("CustomUnetModel", out var modelObj)
                    && modelObj is HybridModelFile model
                )
                {
                    customUnetModel = model;
                    logger.LogInformation("Using custom UNet model: {ModelPath}", model.RelativePath);
                }

                if (
                    request.ProviderOptions.TryGetValue("SelectedLoras", out var lorasObj)
                    && lorasObj is IEnumerable<SelectedLora> loraList
                )
                {
                    loras = loraList;
                    logger.LogInformation("Using {Count} LoRAs", loraList.Count());
                }

                if (request.ProviderOptions.TryGetValue("Width", out var widthObj) && widthObj is int w)
                {
                    width = w;
                }

                if (request.ProviderOptions.TryGetValue("Height", out var heightObj) && heightObj is int h)
                {
                    height = h;
                }

                if (width.HasValue && height.HasValue)
                {
                    logger.LogInformation("Using custom resolution: {Width}x{Height}", width, height);
                }
            }

            // Build workflow nodes
            logger.LogInformation("Building Flux Kontext workflow");
            var nodes = FluxKontextWorkflowBuilder.Build(
                request,
                clientManager,
                customUnetModel,
                loras,
                width,
                height
            );

            // Queue the prompt
            logger.LogInformation("Queuing prompt to ComfyUI");
            var task = await clientManager.Client.QueuePromptAsync(nodes, cancellationToken);

            // Wait for completion
            logger.LogInformation("Waiting for generation to complete (Prompt ID: {PromptId})", task.Id);
            await task.Task.WaitAsync(cancellationToken);

            // Get the output images
            var outputImages = await clientManager.Client.GetImagesForExecutedPromptAsync(
                task.Id,
                cancellationToken
            );

            // Find the SaveImage node output
            var saveImageOutput = outputImages.FirstOrDefault(x => x.Value?.Count > 0);

            if (saveImageOutput.Value == null || saveImageOutput.Value.Count == 0)
            {
                logger.LogWarning("No output images found from generation");
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "No output images were generated",
                };
            }

            // Download the first image
            var comfyImage = saveImageOutput.Value[0];
            logger.LogInformation("Downloading generated image: {FileName}", comfyImage.FileName);

            await using var imageStream = await clientManager.Client.GetImageStreamAsync(
                comfyImage,
                cancellationToken
            );
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);
            var imageBytes = memoryStream.ToArray();
            var base64Image = Convert.ToBase64String(imageBytes);

            var mimeType =
                comfyImage.FileName.EndsWith(".png") ? "image/png"
                : comfyImage.FileName.EndsWith(".jpg") || comfyImage.FileName.EndsWith(".jpeg") ? "image/jpeg"
                : "image/png";

            logger.LogInformation("Successfully generated image with Flux Kontext");

            return new ImageGenerationResponse
            {
                IsSuccess = true,
                Images = [new GeneratedImage { Base64Data = base64Image, MimeType = mimeType }],
                TextResponse = null,
                Metadata = new Dictionary<string, object>
                {
                    ["promptId"] = task.Id,
                    ["fileName"] = comfyImage.FileName,
                },
            };
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Image generation was cancelled");
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage = "Generation was cancelled",
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate image with Flux Kontext");
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Generation failed: {ex.Message}",
            };
        }
    }
}
