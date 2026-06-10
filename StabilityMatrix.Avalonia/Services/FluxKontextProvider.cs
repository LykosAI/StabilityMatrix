using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Flux Kontext using local ComfyUI backend
/// </summary>
public class FluxKontextProvider(ILogger<FluxKontextProvider> logger, IInferenceClientManager clientManager)
    : IImageGenerationProvider
{
    public string ProviderId => BananaVisionProviderIds.FluxKontext;
    public string ProviderName => "Flux Kontext (Local)";
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

            // Upload images using helper
            await ComfyImageUploadHelper.UploadImagesAsync(
                clientManager,
                request,
                maxInputImages: 2,
                providerPrefix: "flux_kontext",
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

            request.Progress?.Report(
                new ImageGenerationProgress(
                    ProviderId,
                    task.Id,
                    Value: null,
                    Maximum: null,
                    RunningNode: null,
                    Stage: "Queued"
                )
            );

            int? lastPercent = null;
            string? lastRunningNode = null;

            void ReportProgress(int? value, int? maximum, string? runningNode, string? stage)
            {
                int? percent = value is >= 0 && maximum is > 0 ? (value.Value * 100) / maximum.Value : null;

                if (
                    percent == lastPercent
                    && string.Equals(lastRunningNode, runningNode, StringComparison.Ordinal)
                )
                {
                    return;
                }

                lastPercent = percent;
                lastRunningNode = runningNode;

                request.Progress?.Report(
                    new ImageGenerationProgress(ProviderId, task.Id, value, maximum, runningNode, stage)
                );
            }

            void OnProgressUpdate(
                object? sender,
                StabilityMatrix.Core.Inference.ComfyProgressUpdateEventArgs args
            )
            {
                ReportProgress(args.Value, args.Maximum, args.RunningNode, "Generating");
            }

            void OnRunningNodeChanged(object? sender, string? node)
            {
                ReportProgress(
                    task.LastProgressUpdate?.Value,
                    task.LastProgressUpdate?.Maximum,
                    node,
                    "Generating"
                );
            }

            task.ProgressUpdate += OnProgressUpdate;
            task.RunningNodeChanged += OnRunningNodeChanged;

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
                    .ContinueWith(_ => interruptCts.Dispose(), TaskScheduler.Default)
                    .SafeFireAndForget();
            });

            try
            {
                // Wait for completion
                logger.LogInformation("Waiting for generation to complete (Prompt ID: {PromptId})", task.Id);
                await task.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                task.ProgressUpdate -= OnProgressUpdate;
                task.RunningNodeChanged -= OnRunningNodeChanged;
            }

            // Get the output images
            var outputImages = await clientManager.Client.GetImagesForExecutedPromptAsync(
                task.Id,
                cancellationToken
            );

            // Prefer the "SaveImage" output node deterministically.
            var preferredOutputKey = "SaveImage";
            string? selectedOutputKey = null;
            List<ComfyImage>? candidateImages = null;

            if (
                outputImages.TryGetValue(preferredOutputKey, out var preferredImages)
                && preferredImages is { Count: > 0 }
            )
            {
                selectedOutputKey = preferredOutputKey;
                candidateImages = preferredImages;
            }
            else
            {
                var selected = outputImages
                    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .FirstOrDefault(kvp => kvp.Value is { Count: > 0 });

                selectedOutputKey = string.IsNullOrEmpty(selected.Key) ? null : selected.Key;
                candidateImages = selected.Value;
            }

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

                var mimeType =
                    comfyImage.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
                    : comfyImage.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || comfyImage.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        ? "image/jpeg"
                    : comfyImage.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
                    : "image/png";

                generatedImages.Add(new GeneratedImage { Base64Data = base64Image, MimeType = mimeType });
            }

            logger.LogInformation(
                "Successfully generated {Count} image(s) with Flux Kontext",
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
        catch (ApiException apiEx)
        {
            // ComfyUI returns a JSON body explaining which node validation failed when the
            // workflow is rejected; surfacing that beats a generic 400 message by miles.
            var detail = !string.IsNullOrWhiteSpace(apiEx.Content) ? apiEx.Content : apiEx.Message;
            logger.LogError(
                apiEx,
                "ComfyUI rejected Flux Kontext workflow ({StatusCode}): {Detail}",
                apiEx.StatusCode,
                detail
            );
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage =
                    $"ComfyUI rejected the workflow ({(int)apiEx.StatusCode}): {Truncate(detail, 800)}",
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

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
