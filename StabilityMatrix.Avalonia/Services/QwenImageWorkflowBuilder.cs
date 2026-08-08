using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Builds ComfyUI workflow nodes for Qwen Image text-to-image generation.
/// Based on the official Comfy-Org "image_qwen_Image_2512" template:
/// UNETLoader -> CLIPLoader(qwen_image) -> VAELoader -> ModelSamplingAuraFlow(3.1) ->
/// EmptySD3LatentImage -> KSampler(euler/simple) -> VAEDecode -> PreviewImage.
/// </summary>
public static class QwenImageWorkflowBuilder
{
    // The official template renders at 50 steps; 20 keeps the conversational surface
    // responsive on consumer cards at a modest quality cost (the model card documents 20
    // as the quick-generation setting) and matches the original Qwen Image guidance.
    private const int DefaultSteps = 20;
    private const double DefaultCfg = 4.0;
    private const double DefaultShift = 3.1;
    private const string DefaultSampler = "euler";
    private const string DefaultScheduler = "simple";
    private const string DefaultClipType = "qwen_image";

    // Qwen Image's native training resolution (the template default).
    private const int DefaultWidth = 1328;
    private const int DefaultHeight = 1328;

    public static Dictionary<string, ComfyNode> Build(
        ImageGenerationRequest request,
        IInferenceClientManager clientManager,
        int? width = null,
        int? height = null,
        int? steps = null,
        double? cfg = null
    )
    {
        var nodes = new NodeDictionary();
        var seed = (ulong)Random.Shared.NextInt64();

        var modelManager = new QwenImageModelManager();
        var selectedModels = modelManager.SelectModels(clientManager);

        var unetModel = selectedModels.UnetModel;
        var isGgufModel = unetModel.RelativePath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase);

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

        var clipOutput = selectedModels.ClipModel.IsGguf
            ? nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.CLIPLoaderGGUF
                    {
                        Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoaderGGUF)),
                        ClipName = selectedModels.ClipModel.RelativePath,
                        Type = DefaultClipType,
                    }
                )
                .Output
            : nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.CLIPLoader
                    {
                        Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                        ClipName = selectedModels.ClipModel.RelativePath,
                        Type = DefaultClipType,
                    }
                )
                .Output;

        var vaeLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = selectedModels.VaeModel.RelativePath,
            }
        );

        var modelSampling = nodes.AddTypedNode(
            new ComfyNodeBuilder.ModelSamplingAuraFlow
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.ModelSamplingAuraFlow)),
                Model = unetOutput,
                Shift = DefaultShift,
            }
        );

        var positiveTextEncode = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPTextEncode)),
                Clip = clipOutput,
                Text = request.TextPrompt ?? "a beautiful image",
            }
        );

        var negativeTextEncode = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = nodes.GetUniqueName("CLIPTextEncode_Negative"),
                Clip = clipOutput,
                Text = string.Empty,
            }
        );

        var emptyLatent = nodes.AddTypedNode(
            new ComfyNodeBuilder.EmptySD3LatentImage
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.EmptySD3LatentImage)),
                Width = width ?? DefaultWidth,
                Height = height ?? DefaultHeight,
                BatchSize = 1,
            }
        );

        var sampler = nodes.AddTypedNode(
            new ComfyNodeBuilder.KSampler
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.KSampler)),
                Model = modelSampling.Output,
                Seed = seed,
                Steps = steps ?? DefaultSteps,
                Cfg = cfg ?? DefaultCfg,
                SamplerName = DefaultSampler,
                Scheduler = DefaultScheduler,
                Positive = positiveTextEncode.Output,
                Negative = negativeTextEncode.Output,
                LatentImage = emptyLatent.Output,
                Denoise = 1.0,
            }
        );

        var vaeDecode = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAEDecode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAEDecode)),
                Samples = sampler.Output,
                Vae = vaeLoader.Output,
            }
        );

        nodes.AddTypedNode(
            new ComfyNodeBuilder.PreviewImage
            {
                Name = nodes.GetUniqueName("SaveImage"),
                Images = vaeDecode.Output,
            }
        );

        return nodes;
    }
}
