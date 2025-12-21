using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Builds ComfyUI workflow nodes for Flux Kontext image generation
/// </summary>
public static class FluxKontextWorkflowBuilder
{
    private const int DefaultWidth = 1024;
    private const int DefaultHeight = 1024;
    private const int DefaultSteps = 25;
    private const double DefaultCfgScale = 3.5;
    private const string DefaultSampler = "euler";
    private const string DefaultScheduler = "simple";

    public static Dictionary<string, ComfyNode> Build(
        ImageGenerationRequest request,
        IInferenceClientManager clientManager,
        HybridModelFile? customUnetModel = null,
        IEnumerable<SelectedLora>? loras = null,
        int? width = null,
        int? height = null
    )
    {
        var nodes = new NodeDictionary();
        var seed = (ulong)Random.Shared.NextInt64();

        // Use provided dimensions or defaults
        var outputWidth = width ?? DefaultWidth;
        var outputHeight = height ?? DefaultHeight;

        // 1. Load models
        var modelManager = new FluxKontextModelManager();
        var selectedModels = modelManager.SelectModels(clientManager);

        // Use custom UNet model if provided, otherwise use the default
        var unetModel = customUnetModel ?? selectedModels.UnetModel;
        var isGgufModel = unetModel.RelativePath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase);

        // UNETLoader - Use GGUF loader for .gguf models
        ModelNodeConnection unetOutput;
        if (isGgufModel)
        {
            var ggufLoader = nodes.AddTypedNode(
                new ComfyNodeBuilder.UnetLoaderGGUF
                {
                    Name = nodes.GetUniqueName("UnetLoaderGGUF"),
                    UnetName = unetModel.RelativePath,
                }
            );
            unetOutput = ggufLoader.Output;
        }
        else
        {
            var unetLoader = nodes.AddTypedNode(
                new ComfyNodeBuilder.UNETLoader
                {
                    Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                    UnetName = unetModel.RelativePath,
                    WeightDtype = "fp8_e4m3fn",
                }
            );
            unetOutput = unetLoader.Output;
        }

        // VAELoader
        var vaeLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = selectedModels.VaeModel.RelativePath,
            }
        );

        // DualCLIPLoader for Flux
        var clipLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.DualCLIPLoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.DualCLIPLoader)),
                ClipName1 = selectedModels.Clip1Model.RelativePath,
                ClipName2 = selectedModels.Clip2Model.RelativePath,
                Type = "flux",
            }
        );

        // Apply LoRAs if any
        var currentModel = unetOutput;
        var currentClip = clipLoader.Output;

        var loraList = loras?.ToList() ?? [];
        if (loraList.Count > 0)
        {
            for (var i = 0; i < loraList.Count; i++)
            {
                var lora = loraList[i];
                var loraLoader = nodes.AddNamedNode(
                    ComfyNodeBuilder.LoraLoader(
                        nodes.GetUniqueName($"LoraLoader_{i + 1}"),
                        currentModel,
                        currentClip,
                        lora.Model.RelativePath,
                        (double)lora.ModelWeight,
                        (double)lora.ClipWeight
                    )
                );
                currentModel = loraLoader.Output1;
                currentClip = loraLoader.Output2;
            }
        }

        // 2. Encode text prompt
        var positivePrompt = request.TextPrompt ?? "a beautiful image";

        var clipTextEncode = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPTextEncode)),
                Clip = currentClip,
                Text = positivePrompt,
            }
        );

        // 3. Setup latent source and conditioning (with optional reference images)
        LatentNodeConnection latentSource;
        ConditioningNodeConnection conditioningForGuidance;

        // Collect reference images (from input and conversation history)
        var referenceImageNames = GetReferenceImageNames(request);

        if (referenceImageNames.Count > 0)
        {
            // Load and process reference images
            ImageNodeConnection? combinedImage = null;

            if (referenceImageNames.Count == 1)
            {
                // Single image - just load it
                var loadImage = nodes.AddTypedNode(
                    new ComfyNodeBuilder.LoadImage
                    {
                        Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.LoadImage)),
                        Image = referenceImageNames[0],
                    }
                );
                combinedImage = loadImage.Output1;
            }
            else
            {
                // Multiple images - load and stitch them
                var firstImage = nodes.AddTypedNode(
                    new ComfyNodeBuilder.LoadImage
                    {
                        Name = nodes.GetUniqueName("LoadImage_1"),
                        Image = referenceImageNames[0],
                    }
                );

                var secondImage = nodes.AddTypedNode(
                    new ComfyNodeBuilder.LoadImage
                    {
                        Name = nodes.GetUniqueName("LoadImage_2"),
                        Image = referenceImageNames[1],
                    }
                );

                // Stitch images together
                var imageStitch = nodes.AddTypedNode(
                    new ComfyNodeBuilder.ImageStitch
                    {
                        Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.ImageStitch)),
                        Image1 = firstImage.Output1,
                        Image2 = secondImage.Output1,
                        Direction = "right",
                        MatchImageSize = true,
                        SpacingWidth = 0,
                        SpacingColor = "white",
                    }
                );

                combinedImage = imageStitch.Output;
            }

            // Scale image to Flux Kontext target resolution (auto-scales to correct size)
            var imageScale = nodes.AddTypedNode(
                new ComfyNodeBuilder.FluxKontextImageScale
                {
                    Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.FluxKontextImageScale)),
                    Image = combinedImage!,
                }
            );

            // Encode image to latent space
            var vaeEncode = nodes.AddTypedNode(
                new ComfyNodeBuilder.VAEEncode
                {
                    Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAEEncode)),
                    Pixels = imageScale.Output,
                    Vae = vaeLoader.Output,
                }
            );

            // Use encoded image as latent source
            latentSource = vaeEncode.Output;

            // Add reference latent to conditioning for style consistency
            var referenceLatent = nodes.AddTypedNode(
                new ComfyNodeBuilder.ReferenceLatent
                {
                    Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.ReferenceLatent)),
                    Conditioning = clipTextEncode.Output,
                    Latent = vaeEncode.Output,
                }
            );

            // Use reference latent output for conditioning
            conditioningForGuidance = referenceLatent.Output;
        }
        else
        {
            // No reference images - pure text-to-image with empty latent
            var emptyLatent = nodes.AddTypedNode(
                new ComfyNodeBuilder.EmptySD3LatentImage
                {
                    Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.EmptySD3LatentImage)),
                    Width = outputWidth,
                    Height = outputHeight,
                    BatchSize = 1,
                }
            );
            latentSource = emptyLatent.Output;

            // Use text encoding directly for conditioning
            conditioningForGuidance = clipTextEncode.Output;
        }

        // 4. Flux Guidance (uses conditioning from above - either direct or with reference)
        var fluxGuidance = nodes.AddTypedNode(
            new ComfyNodeBuilder.FluxGuidance
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.FluxGuidance)),
                Conditioning = conditioningForGuidance,
                Guidance = DefaultCfgScale,
            }
        );

        // 5. BasicGuider
        var basicGuider = nodes.AddTypedNode(
            new ComfyNodeBuilder.BasicGuider
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.BasicGuider)),
                Model = currentModel,
                Conditioning = fluxGuidance.Output,
            }
        );

        // 6. KSamplerSelect
        var samplerSelect = nodes.AddTypedNode(
            new ComfyNodeBuilder.KSamplerSelect
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.KSamplerSelect)),
                SamplerName = DefaultSampler,
            }
        );

        // 7. RandomNoise
        var randomNoise = nodes.AddTypedNode(
            new ComfyNodeBuilder.RandomNoise
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.RandomNoise)),
                NoiseSeed = seed,
            }
        );

        // 8. BasicScheduler
        var basicScheduler = nodes.AddTypedNode(
            new ComfyNodeBuilder.BasicScheduler
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.BasicScheduler)),
                Model = currentModel,
                Scheduler = DefaultScheduler,
                Steps = DefaultSteps,
                Denoise = 1.0,
            }
        );

        // 9. SamplerCustomAdvanced
        var sampler = nodes.AddTypedNode(
            new ComfyNodeBuilder.SamplerCustomAdvanced
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.SamplerCustomAdvanced)),
                Noise = randomNoise.Output,
                Guider = basicGuider.Output,
                Sampler = samplerSelect.Output,
                Sigmas = basicScheduler.Output,
                LatentImage = latentSource,
            }
        );

        // 10. VAEDecode
        var vaeDecode = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAEDecode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAEDecode)),
                Samples = sampler.Output1,
                Vae = vaeLoader.Output,
            }
        );

        // 11. PreviewImage (output node - images are retrieved from ComfyUI after execution)
        var previewImage = nodes.AddTypedNode(
            new ComfyNodeBuilder.PreviewImage
            {
                Name = nodes.GetUniqueName("SaveImage"),
                Images = vaeDecode.Output,
            }
        );

        // Return the node dictionary directly
        return nodes;
    }

    /// <summary>
    /// Get reference image filenames from the request (input images + conversation history)
    /// Note: Images uploaded via ComfyClient.UploadImageAsync go to the "Inference" subfolder
    /// </summary>
    private static List<string> GetReferenceImageNames(ImageGenerationRequest request)
    {
        var imageNames = new List<string>();

        // Priority 1: Current input images (will be uploaded with known names)
        if (request.InputImages?.Count > 0)
        {
            for (var i = 0; i < Math.Min(request.InputImages.Count, 2); i++) // Max 2 images
            {
                // Include the "Inference/" subfolder prefix since that's where UploadImageAsync uploads to
                imageNames.Add($"Inference/flux_kontext_input_{i}.png");
            }
        }

        // Priority 2: Most recent image from conversation history (previous generation)
        // Only include if we actually have the image content to upload
        if (imageNames.Count < 2 && request.ConversationHistory != null)
        {
            var lastAssistantImage = request.ConversationHistory.LastOrDefault(m =>
                m is { Role: MessageRole.Assistant, ImageContent: not null }
            );

            // Only add the filename if we found an image with content
            // (the provider will have uploaded it)
            if (lastAssistantImage?.ImageContent != null)
            {
                // Include the "Inference/" subfolder prefix
                imageNames.Add("Inference/flux_kontext_history_latest.png");
            }
        }

        return imageNames;
    }

    /// <summary>
    /// Selected models for Flux Kontext
    /// </summary>
    internal record SelectedModels(
        HybridModelFile UnetModel,
        HybridModelFile VaeModel,
        HybridModelFile Clip1Model,
        HybridModelFile Clip2Model
    );
}
