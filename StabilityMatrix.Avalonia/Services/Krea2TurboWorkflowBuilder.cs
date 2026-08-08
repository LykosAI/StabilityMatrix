using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Builds ComfyUI workflow nodes for Krea 2 Turbo text-to-image generation.
/// Based on the official Comfy-Org Krea-2 template (flattened):
/// UNETLoader -> CLIPLoader(krea2) -> VAELoader -> EmptySD3LatentImage ->
/// KSampler(euler/simple, 8 steps, CFG 1) -> VAEDecode -> PreviewImage.
/// No model-sampling shift node — the reference workflow doesn't use one.
/// </summary>
public static class Krea2TurboWorkflowBuilder
{
    // Turbo is an 8-step distilled checkpoint tuned for minimal guidance; CFG above 1
    // visibly degrades output.
    private const int DefaultSteps = 8;
    private const double DefaultCfg = 1.0;
    private const string DefaultSampler = "euler";
    private const string DefaultScheduler = "simple";
    private const string DefaultClipType = "krea2";

    private const int DefaultWidth = 1024;
    private const int DefaultHeight = 1024;

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

        var modelManager = new Krea2TurboModelManager();
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
                Model = unetOutput,
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
