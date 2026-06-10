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
/// Image generation provider for Flux.2 Klein using local ComfyUI backend.
/// Klein 4B is Apache 2.0 licensed; the distilled variant runs at 4 steps with CFG=1
/// making it well-suited to conversational, iterative editing.
/// </summary>
public class Flux2KleinProvider(ILogger<Flux2KleinProvider> logger, IInferenceClientManager clientManager)
    : IImageGenerationProvider
{
    public string ProviderId => BananaVisionProviderIds.Flux2Klein;
    public string ProviderName => "Flux.2 Klein (Local)";
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

            // Resolve the user's UNET selection first — the availability check below is
            // variant-aware (a 9B UNET needs the qwen_3_8b encoder, 4B needs qwen_3_4b),
            // so it has to know which UNET the workflow will actually use.
            HybridModelFile? customUnetModel = null;
            if (
                request.ProviderOptions?.TryGetValue("CustomUnetModel", out var modelObj) == true
                && modelObj is HybridModelFile model
            )
            {
                customUnetModel = model;
                logger.LogInformation("Using custom UNet model: {ModelPath}", model.RelativePath);
            }

            var modelManager = new Flux2KleinModelManager();
            if (!modelManager.AreModelsAvailable(clientManager, customUnetModel))
            {
                var modelsList = string.Join(
                    ", ",
                    modelManager.GetMissingModelNames(clientManager, customUnetModel)
                );

                logger.LogWarning("Required models not found: {Models}", modelsList);
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage =
                        $"Required models not found: {modelsList}. Please download them from the HuggingFace model browser.",
                };
            }

            // Klein supports multi-reference editing; cap at 4 for predictable VRAM use.
            await ComfyImageUploadHelper.UploadImagesAsync(
                clientManager,
                request,
                maxInputImages: 4,
                providerPrefix: "flux2_klein",
                logger,
                cancellationToken
            );

            IEnumerable<SelectedLora>? loras = null;
            int? width = null;
            int? height = null;
            int? steps = null;
            double? cfg = null;
            var explicitDimensions = false;

            if (request.ProviderOptions != null)
            {
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

                if (
                    request.ProviderOptions.TryGetValue("ExplicitDimensions", out var explicitObj)
                    && explicitObj is bool eb
                )
                {
                    explicitDimensions = eb;
                }

                if (request.ProviderOptions.TryGetValue("Steps", out var stepsObj) && stepsObj is int s)
                {
                    steps = s;
                }

                if (request.ProviderOptions.TryGetValue("CfgScale", out var cfgObj))
                {
                    cfg = cfgObj switch
                    {
                        double d => d,
                        float f => f,
                        int i => i,
                        _ => null,
                    };
                }

                if (width.HasValue && height.HasValue)
                {
                    logger.LogInformation("Using custom resolution: {Width}x{Height}", width, height);
                }
                if (steps.HasValue || cfg.HasValue)
                {
                    logger.LogInformation("Using Klein overrides: Steps={Steps}, Cfg={Cfg}", steps, cfg);
                }
            }

            logger.LogInformation("Building Flux.2 Klein workflow");
            var nodes = Flux2KleinWorkflowBuilder.Build(
                request,
                clientManager,
                customUnetModel,
                loras,
                width,
                height,
                steps,
                cfg,
                explicitDimensions
            );

            logger.LogInformation("Queuing prompt to ComfyUI");
            var task = await clientManager.Client.QueuePromptAsync(nodes, cancellationToken);

            // Reports "Queued" now, then deduplicated progress updates until disposed
            using var progressReporter = new ComfyProgressReporter(task, ProviderId, request.Progress);

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

            logger.LogInformation("Waiting for generation to complete (Prompt ID: {PromptId})", task.Id);
            await task.Task.WaitAsync(cancellationToken);

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
                "Successfully generated {Count} image(s) with Flux.2 Klein",
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
            throw;
        }
        catch (ComfyNodeException nodeEx)
        {
            // Execution-time node failure (e.g. tensor-shape mismatch from a wrong encoder
            // pairing). Carries ComfyUI's full error JSON for the detail dialog.
            return ComfyGenerationHelper.CreateNodeErrorResponse(nodeEx, "Flux.2 Klein", logger);
        }
        catch (ApiException apiEx)
        {
            // Queue-time rejection; ComfyUI's JSON body explains which node validation failed.
            return ComfyGenerationHelper.CreateWorkflowRejectedResponse(apiEx, "Flux.2 Klein", logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate image with Flux.2 Klein");
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Generation failed: {ex.Message}",
            };
        }
    }
}
