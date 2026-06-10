using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Qwen Image Edit using local ComfyUI backend
/// </summary>
public class QwenImageEditProvider(
    ILogger<QwenImageEditProvider> logger,
    IInferenceClientManager clientManager
) : IImageGenerationProvider
{
    public string ProviderId => BananaVisionProviderIds.QwenImageEdit;
    public string ProviderName => "Qwen Image Edit (Local)";
    public bool SupportsImageInput => true;
    public bool SupportsMultiTurn => true;
    public bool RequiresThoughtSignatures => false;

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
                        "ComfyUI is not connected. Use the Launch button in the header to start and connect to ComfyUI.",
                };
            }

            // Validate models are available
            var modelManager = new QwenImageEditModelManager();
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

            // Upload images using helper
            await ComfyImageUploadHelper.UploadImagesAsync(
                clientManager,
                request,
                maxInputImages: 3,
                providerPrefix: "qwen_image_edit",
                logger,
                cancellationToken
            );

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
            logger.LogInformation("Building Qwen Image Edit workflow");
            var nodes = QwenImageEditWorkflowBuilder.Build(
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

            // Reports "Queued" now, then deduplicated progress updates until disposed
            using var progressReporter = new ComfyProgressReporter(task, ProviderId, request.Progress);

            // Register cancellation to interrupt ComfyUI if the user cancels
            await using var promptInterrupt = cancellationToken.Register(() =>
            {
                logger.LogInformation(
                    "Cancellation requested, interrupting ComfyUI prompt {PromptId}",
                    task.Id
                );
                // CTS holds an internal timer that needs disposing; chain dispose onto
                // the fire-and-forget interrupt so it cleans up once the request settles.
                var interruptCts = new CancellationTokenSource(5000);
                clientManager
                    .Client.InterruptPromptAsync(interruptCts.Token)
                    .ContinueWith(
                        t =>
                        {
                            if (t.IsFaulted)
                            {
                                logger.LogWarning(
                                    t.Exception,
                                    "Failed to interrupt ComfyUI prompt {PromptId}",
                                    task.Id
                                );
                            }
                            interruptCts.Dispose();
                        },
                        TaskScheduler.Default
                    )
                    .SafeFireAndForget();
            });

            // Wait for completion
            logger.LogInformation("Waiting for generation to complete (Prompt ID: {PromptId})", task.Id);
            await task.Task.WaitAsync(cancellationToken);

            // Get the output images
            var outputImages = await clientManager.Client.GetImagesForExecutedPromptAsync(
                task.Id,
                cancellationToken
            );

            var (selectedOutputKey, candidateImages) = ComfyGenerationHelper.SelectOutputImages(outputImages);

            if (candidateImages is null || candidateImages.Count == 0)
            {
                logger.LogWarning("No output images found from generation");
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "No output images were generated",
                };
            }

            var generatedImages = new List<GeneratedImage>();

            foreach (var comfyImage in candidateImages)
            {
                logger.LogInformation("Downloading generated image: {FileName}", comfyImage.FileName);

                await using var imageStream = await clientManager.Client.GetImageStreamAsync(
                    comfyImage,
                    cancellationToken
                );
                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream, cancellationToken);
                var imageBytes = memoryStream.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);

                var mimeType = ComfyGenerationHelper.GetMimeTypeForFileName(comfyImage.FileName);

                generatedImages.Add(new GeneratedImage { Base64Data = base64Image, MimeType = mimeType });
            }

            logger.LogInformation(
                "Successfully generated {Count} image(s) with Qwen Image Edit",
                generatedImages.Count
            );

            return new ImageGenerationResponse
            {
                IsSuccess = true,
                Images = generatedImages,
                TextResponse = null,
                Metadata = new Dictionary<string, object>
                {
                    ["promptId"] = task.Id,
                    ["outputNode"] = selectedOutputKey ?? "unknown",
                },
            };
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Image generation was cancelled");
            throw; // Propagate cancellation to ViewModel for proper handling
        }
        catch (ComfyNodeException nodeEx)
        {
            // Execution-time node failure (e.g. tensor-shape mismatch from a wrong encoder
            // pairing). Carries ComfyUI's full error JSON for the detail dialog.
            return ComfyGenerationHelper.CreateNodeErrorResponse(nodeEx, "Qwen Image Edit", logger);
        }
        catch (ApiException apiEx)
        {
            // Queue-time rejection; ComfyUI's JSON body explains which node validation failed.
            return ComfyGenerationHelper.CreateWorkflowRejectedResponse(apiEx, "Qwen Image Edit", logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate image with Qwen Image Edit");
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Generation failed: {ex.Message}",
            };
        }
    }
}
