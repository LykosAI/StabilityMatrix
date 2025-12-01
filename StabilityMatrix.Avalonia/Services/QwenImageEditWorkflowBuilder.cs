using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Builds ComfyUI workflow nodes for Qwen Image Edit generation
/// </summary>
public static class QwenImageEditWorkflowBuilder
{
    private const int DefaultWidth = 1024;
    private const int DefaultHeight = 1024;
    private const int DefaultSteps = 20;
    private const double DefaultCfgScale = 4.0;
    private const double DefaultShift = 3.1;
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
        var modelManager = new QwenImageEditModelManager();
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
                    WeightDtype = "default",
                }
            );
            unetOutput = unetLoader.Output;
        }

        // Apply ModelSamplingAuraFlow to the model
        var modelSampling = nodes.AddTypedNode(
            new ComfyNodeBuilder.ModelSamplingAuraFlow
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.ModelSamplingAuraFlow)),
                Model = unetOutput,
                Shift = DefaultShift,
            }
        );
        var currentModel = modelSampling.Output;

        // VAELoader
        var vaeLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = selectedModels.VaeModel.RelativePath,
            }
        );

        // CLIPLoader for Qwen (type: "qwen_image")
        var clipLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPLoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                ClipName = selectedModels.ClipModel.RelativePath,
                Type = "qwen_image",
            }
        );
        var currentClip = clipLoader.Output;

        // Apply LoRAs if any
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

        // Get reference image filenames (up to 3 for Qwen)
        var referenceImageNames = GetReferenceImageNames(request);

        // Load reference images if any
        ImageNodeConnection? image1 = null;
        ImageNodeConnection? image2 = null;
        ImageNodeConnection? image3 = null;

        if (referenceImageNames.Count >= 1)
        {
            var loadImage1 = nodes.AddTypedNode(
                new ComfyNodeBuilder.LoadImage
                {
                    Name = nodes.GetUniqueName("LoadImage_1"),
                    Image = referenceImageNames[0],
                }
            );
            image1 = loadImage1.Output1;
        }

        if (referenceImageNames.Count >= 2)
        {
            var loadImage2 = nodes.AddTypedNode(
                new ComfyNodeBuilder.LoadImage
                {
                    Name = nodes.GetUniqueName("LoadImage_2"),
                    Image = referenceImageNames[1],
                }
            );
            image2 = loadImage2.Output1;
        }

        if (referenceImageNames.Count >= 3)
        {
            var loadImage3 = nodes.AddTypedNode(
                new ComfyNodeBuilder.LoadImage
                {
                    Name = nodes.GetUniqueName("LoadImage_3"),
                    Image = referenceImageNames[2],
                }
            );
            image3 = loadImage3.Output1;
        }

        // 2. Encode text prompts with Qwen text encoder
        var positivePrompt = request.TextPrompt ?? "a beautiful image";

        // Positive prompt with images
        var positiveEncode = nodes.AddTypedNode(
            new ComfyNodeBuilder.TextEncodeQwenImageEditPlus
            {
                Name = nodes.GetUniqueName("TextEncodeQwenImageEditPlus_Positive"),
                Clip = currentClip,
                Vae = vaeLoader.Output,
                Image1 = image1,
                Image2 = image2,
                Image3 = image3,
                Prompt = positivePrompt,
            }
        );

        // Negative prompt (empty, with same images for consistency)
        var negativeEncode = nodes.AddTypedNode(
            new ComfyNodeBuilder.TextEncodeQwenImageEditPlus
            {
                Name = nodes.GetUniqueName("TextEncodeQwenImageEditPlus_Negative"),
                Clip = currentClip,
                Vae = vaeLoader.Output,
                Image1 = image1,
                Image2 = image2,
                Image3 = image3,
                Prompt = "",
            }
        );

        // 3. Create empty latent
        var emptyLatent = nodes.AddTypedNode(
            new ComfyNodeBuilder.EmptySD3LatentImage
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.EmptySD3LatentImage)),
                Width = outputWidth,
                Height = outputHeight,
                BatchSize = 1,
            }
        );

        // 4. KSampler
        var sampler = nodes.AddTypedNode(
            new ComfyNodeBuilder.KSampler
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.KSampler)),
                Model = currentModel,
                Seed = seed,
                Steps = DefaultSteps,
                Cfg = DefaultCfgScale,
                SamplerName = DefaultSampler,
                Scheduler = DefaultScheduler,
                Positive = positiveEncode.Output,
                Negative = negativeEncode.Output,
                LatentImage = emptyLatent.Output,
                Denoise = 1.0,
            }
        );

        // 5. VAEDecode
        var vaeDecode = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAEDecode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAEDecode)),
                Samples = sampler.Output,
                Vae = vaeLoader.Output,
            }
        );

        // 6. PreviewImage (output node - images are retrieved from ComfyUI after execution)
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
        // Qwen supports up to 3 images natively
        if (request.InputImages?.Count > 0)
        {
            for (var i = 0; i < Math.Min(request.InputImages.Count, 3); i++) // Max 3 images for Qwen
            {
                // Include the "Inference/" subfolder prefix since that's where UploadImageAsync uploads to
                imageNames.Add($"Inference/qwen_image_edit_input_{i}.png");
            }
        }

        // Priority 2: Most recent image from conversation history (previous generation)
        // Only include if we have room (less than 3 images)
        if (imageNames.Count < 3 && request.ConversationHistory != null)
        {
            var lastAssistantImage = request.ConversationHistory.LastOrDefault(m =>
                m is { Role: MessageRole.Assistant, ImageContent: not null }
            );

            // Only add the filename if we found an image with content
            // (the provider will have uploaded it)
            if (lastAssistantImage?.ImageContent != null)
            {
                // Include the "Inference/" subfolder prefix
                imageNames.Add("Inference/qwen_image_edit_history_latest.png");
            }
        }

        return imageNames;
    }
}
