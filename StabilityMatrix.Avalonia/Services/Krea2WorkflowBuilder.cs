using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Builds the ComfyUI node graph for Krea 2 text-to-image:
/// UNETLoader -> CLIPLoader(type=krea2, Qwen3-VL-4B) -> VAELoader(Wan 2.1) -> LoRAs ->
/// CLIPTextEncode (+ empty negative) -> EmptySD3LatentImage -> KSampler -> VAEDecode -> PreviewImage.
/// Defaults match the Turbo variant: euler / simple, 8 steps, CFG 1. The model patches shift
/// internally, so no ModelSamplingAuraFlow-style node is added.
/// </summary>
public static class Krea2WorkflowBuilder
{
    internal const int DefaultWidth = 1024;
    internal const int DefaultHeight = 1024;
    internal const int DefaultSteps = 8;
    internal const double DefaultCfg = 1.0;
    internal const string ClipType = "krea2";
    private const string Sampler = "euler";
    private const string Scheduler = "simple";

    public static Dictionary<string, ComfyNode> Build(
        ImageGenerationRequest request,
        IInferenceClientManager clientManager,
        HybridModelFile? customUnetModel = null,
        IEnumerable<SelectedLora>? loras = null,
        int? width = null,
        int? height = null,
        int? steps = null,
        double? cfg = null
    )
    {
        var nodes = new NodeDictionary();
        var seed = (ulong)Random.Shared.NextInt64();

        var selected = new Krea2ModelManager().SelectModels(clientManager, customUnetModel);

        ModelNodeConnection unetOutput;
        if (selected.UnetModel.RelativePath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            unetOutput = nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.UnetLoaderGGUF
                    {
                        Name = nodes.GetUniqueName("UnetLoaderGGUF"),
                        UnetName = selected.UnetModel.RelativePath,
                    }
                )
                .Output;
        }
        else
        {
            unetOutput = nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.UNETLoader
                    {
                        Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                        UnetName = selected.UnetModel.RelativePath,
                        WeightDtype = "default",
                    }
                )
                .Output;
        }

        // Krea 2 uses one CLIPLoader with the krea2 type — never the dual loader.
        var clipOutput = selected.ClipModel.IsGguf
            ? nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.CLIPLoaderGGUF
                    {
                        Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoaderGGUF)),
                        ClipName = selected.ClipModel.RelativePath,
                        Type = ClipType,
                    }
                )
                .Output
            : nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.CLIPLoader
                    {
                        Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                        ClipName = selected.ClipModel.RelativePath,
                        Type = ClipType,
                    }
                )
                .Output;

        var vaeLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = selected.VaeModel.RelativePath,
            }
        );

        var (currentModel, currentClip) = ComfyWorkflowHelper.ApplyLoras(nodes, loras, unetOutput, clipOutput);

        var positive = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPTextEncode)),
                Clip = currentClip,
                Text = request.TextPrompt ?? "a beautiful image",
            }
        );

        // Separate empty-string encode as the negative (CFG 1 ignores it, but a base variant
        // at CFG > 1 needs a real baseline rather than a zeroed conditioning).
        var negative = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = nodes.GetUniqueName("CLIPTextEncode_Negative"),
                Clip = currentClip,
                Text = string.Empty,
            }
        );

        var latent = nodes.AddTypedNode(
            new ComfyNodeBuilder.EmptySD3LatentImage
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.EmptySD3LatentImage)),
                Width = width ?? DefaultWidth,
                Height = height ?? DefaultHeight,
                BatchSize = 1,
            }
        );

        var sampled = nodes.AddTypedNode(
            new ComfyNodeBuilder.KSampler
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.KSampler)),
                Model = currentModel,
                Seed = seed,
                Steps = steps ?? DefaultSteps,
                Cfg = cfg ?? DefaultCfg,
                SamplerName = Sampler,
                Scheduler = Scheduler,
                Positive = positive.Output,
                Negative = negative.Output,
                LatentImage = latent.Output,
                Denoise = 1.0,
            }
        );

        var decoded = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAEDecode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAEDecode)),
                Samples = sampled.Output,
                Vae = vaeLoader.Output,
            }
        );

        nodes.AddTypedNode(
            new ComfyNodeBuilder.PreviewImage
            {
                Name = nodes.GetUniqueName("SaveImage"),
                Images = decoded.Output,
            }
        );

        return nodes;
    }
}
