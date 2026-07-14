using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Base class for Image Lab providers that generate via a local ComfyUI backend.
/// Implements the shared generation flow as a template method — connection check,
/// model validation, image upload, prompt queue + progress reporting, cancellation
/// interrupt, output download, and error handling — so subclasses supply only the
/// provider-specific model requirements and workflow node graph.
/// </summary>
public abstract class ComfyImageGenerationProviderBase(ILogger logger, IInferenceClientManager clientManager)
    : IImageGenerationProvider
{
    protected ILogger Logger { get; } = logger;
    protected IInferenceClientManager ClientManager { get; } = clientManager;

    public abstract string ProviderId { get; }
    public abstract string ProviderName { get; }

    public virtual bool SupportsImageInput => true;
    public virtual bool SupportsMultiTurn => true;
    public virtual bool RequiresThoughtSignatures => false;

    /// <summary>Short name used in log messages and error responses (e.g. "Flux Kontext").</summary>
    protected abstract string LogName { get; }

    /// <summary>Maximum number of input images uploaded to ComfyUI for this provider.</summary>
    protected abstract int MaxInputImages { get; }

    /// <summary>Filename prefix for uploaded images (e.g. "flux_kontext").</summary>
    protected abstract string ProviderPrefix { get; }

    /// <summary>
    /// Returns the names of any required models that are missing. An empty list means all
    /// required models are available and generation may proceed.
    /// </summary>
    protected abstract IReadOnlyList<string> GetMissingModels(ImageGenerationRequest request);

    /// <summary>
    /// Extracts provider options from the request and builds the ComfyUI workflow node graph.
    /// </summary>
    protected abstract Dictionary<string, ComfyNode> BuildWorkflow(ImageGenerationRequest request);

    public async Task<ImageGenerationResponse> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!ClientManager.IsConnected)
            {
                Logger.LogWarning("ComfyUI is not connected");
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage =
                        "ComfyUI is not connected. Use the Launch button in the header to start and connect to ComfyUI.",
                };
            }

            var missingModels = GetMissingModels(request);
            if (missingModels.Count > 0)
            {
                var modelsList = string.Join(", ", missingModels);
                Logger.LogWarning("Required models not found: {Models}", modelsList);
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage =
                        $"Required models not found: {modelsList}. Please download them from the HuggingFace model browser.",
                };
            }

            await ComfyImageUploadHelper.UploadImagesAsync(
                ClientManager,
                request,
                MaxInputImages,
                ProviderPrefix,
                Logger,
                cancellationToken
            );

            Logger.LogInformation("Building {Provider} workflow", LogName);
            var nodes = BuildWorkflow(request);

            Logger.LogInformation("Queuing prompt to ComfyUI");
            var task = await ClientManager.Client.QueuePromptAsync(nodes, cancellationToken);

            // Reports "Queued" now, then deduplicated progress updates until disposed
            using var progressReporter = new ComfyProgressReporter(task, ProviderId, request.Progress);

            // Interrupt the running ComfyUI prompt if the user cancels
            await using var promptInterrupt = RegisterPromptInterrupt(task, cancellationToken);

            Logger.LogInformation("Waiting for generation to complete (Prompt ID: {PromptId})", task.Id);
            await task.Task.WaitAsync(cancellationToken);

            var outputImages = await ClientManager.Client.GetImagesForExecutedPromptAsync(
                task.Id,
                cancellationToken
            );

            var (selectedOutputKey, candidateImages) = ComfyGenerationHelper.SelectOutputImages(outputImages);

            if (candidateImages is null || candidateImages.Count == 0)
            {
                Logger.LogWarning("No output images found from generation");
                return new ImageGenerationResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "No output images were generated",
                };
            }

            var generatedImages = await DownloadImagesAsync(candidateImages, cancellationToken);

            Logger.LogInformation(
                "Successfully generated {Count} image(s) with {Provider}",
                generatedImages.Count,
                LogName
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
            Logger.LogInformation("Image generation was cancelled");
            throw; // Propagate cancellation to the ViewModel for proper handling
        }
        catch (ComfyNodeException nodeEx)
        {
            // Execution-time node failure (e.g. tensor-shape mismatch from a wrong encoder
            // pairing). Carries ComfyUI's full error JSON for the detail dialog.
            return ComfyGenerationHelper.CreateNodeErrorResponse(nodeEx, LogName, Logger);
        }
        catch (ApiException apiEx)
        {
            // Queue-time rejection; ComfyUI's JSON body explains which node validation failed.
            return ComfyGenerationHelper.CreateWorkflowRejectedResponse(apiEx, LogName, Logger);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate image with {Provider}", LogName);
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Generation failed: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Registers a cancellation callback that interrupts the running ComfyUI prompt.
    /// </summary>
    private CancellationTokenRegistration RegisterPromptInterrupt(
        ComfyTask task,
        CancellationToken cancellationToken
    ) =>
        cancellationToken.Register(() =>
        {
            Logger.LogInformation("Cancellation requested, interrupting ComfyUI prompt {PromptId}", task.Id);
            // CTS holds an internal timer that needs disposing; chain dispose onto the
            // fire-and-forget interrupt so it cleans up once the request settles.
            var interruptCts = new CancellationTokenSource(5000);
            ClientManager
                .Client.InterruptPromptAsync(interruptCts.Token)
                .ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                        {
                            Logger.LogWarning(
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

    /// <summary>
    /// Downloads each output image from ComfyUI and converts it to a base64 GeneratedImage.
    /// </summary>
    private async Task<List<GeneratedImage>> DownloadImagesAsync(
        IReadOnlyList<ComfyImage> images,
        CancellationToken cancellationToken
    )
    {
        var generatedImages = new List<GeneratedImage>();

        foreach (var comfyImage in images)
        {
            Logger.LogInformation("Downloading generated image: {FileName}", comfyImage.FileName);

            await using var imageStream = await ClientManager.Client.GetImageStreamAsync(
                comfyImage,
                cancellationToken
            );
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);
            var base64Image = Convert.ToBase64String(memoryStream.ToArray());

            generatedImages.Add(
                new GeneratedImage
                {
                    Base64Data = base64Image,
                    MimeType = ComfyGenerationHelper.GetMimeTypeForFileName(comfyImage.FileName),
                }
            );
        }

        return generatedImages;
    }

    /// <summary>
    /// Reads the optional custom UNET model selection from the request options.
    /// </summary>
    /// <param name="request">Generation request</param>
    /// <param name="logSelection">
    /// Whether to log the selection. Variant-aware providers resolve the UNET during model
    /// validation as well as workflow build, and pass <c>false</c> on the validation pass to
    /// avoid logging the same selection twice.
    /// </param>
    protected HybridModelFile? GetCustomUnetModel(ImageGenerationRequest request, bool logSelection = true)
    {
        if (
            request.ProviderOptions?.TryGetValue("CustomUnetModel", out var modelObj) == true
            && modelObj is HybridModelFile model
        )
        {
            if (logSelection)
            {
                Logger.LogInformation("Using custom UNet model: {ModelPath}", model.RelativePath);
            }
            return model;
        }

        return null;
    }

    /// <summary>
    /// Reads the optional LoRA selections from the request options.
    /// </summary>
    protected IReadOnlyList<SelectedLora>? GetSelectedLoras(ImageGenerationRequest request)
    {
        if (
            request.ProviderOptions?.TryGetValue("SelectedLoras", out var lorasObj) == true
            && lorasObj is IEnumerable<SelectedLora> loraList
        )
        {
            var loras = loraList.ToList();
            Logger.LogInformation("Using {Count} LoRAs", loras.Count);
            return loras;
        }

        return null;
    }

    /// <summary>
    /// Reads the optional custom output dimensions from the request options.
    /// </summary>
    protected (int? Width, int? Height) GetDimensions(ImageGenerationRequest request)
    {
        int? width = null;
        int? height = null;

        if (request.ProviderOptions?.TryGetValue("Width", out var widthObj) == true && widthObj is int w)
        {
            width = w;
        }

        if (request.ProviderOptions?.TryGetValue("Height", out var heightObj) == true && heightObj is int h)
        {
            height = h;
        }

        if (width.HasValue && height.HasValue)
        {
            Logger.LogInformation("Using custom resolution: {Width}x{Height}", width, height);
        }

        return (width, height);
    }
}
