using SkiaSharp;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Builds ComfyUI workflow nodes for Flux.2 Klein image-edit generation.
/// Based on the official Comfy-Org "image_flux2_klein_image_edit_4b_distilled" workflow:
/// UNETLoader -> CLIPLoader(flux2) -> VAELoader -> (per ref image: LoadImage ->
/// ImageScaleToTotalPixels -> VAEEncode -> ReferenceLatent(positive) +
/// ReferenceLatent(negative)) -> CFGGuider(cfg=1) -> Flux2Scheduler(4 steps) ->
/// SamplerCustomAdvanced -> VAEDecode -> PreviewImage.
/// </summary>
public static class Flux2KleinWorkflowBuilder
{
    private const int DefaultWidth = 1024;
    private const int DefaultHeight = 1024;
    private const int DefaultSteps = 4;
    private const double DefaultCfg = 1.0;
    private const string DefaultSampler = "euler";
    private const string DefaultClipType = "flux2";
    private const string DefaultUpscaleMethod = "nearest-exact";
    private const double ReferenceMegapixels = 1.0;
    private const int ReferenceResolutionStep = 16;
    private const double TargetOutputMegapixels = 1.0;

    public static Dictionary<string, ComfyNode> Build(
        ImageGenerationRequest request,
        IInferenceClientManager clientManager,
        HybridModelFile? customUnetModel = null,
        IEnumerable<SelectedLora>? loras = null,
        int? width = null,
        int? height = null,
        int? steps = null,
        double? cfg = null,
        bool explicitDimensions = false
    )
    {
        var nodes = new NodeDictionary();
        var seed = (ulong)Random.Shared.NextInt64();

        // Steps and CFG default to the distilled values (4 / 1) — base variants need
        // 20 / 5. The VM auto-detects and overrides via providerOptions when the user
        // picks a base model, and the user can manually adjust via the settings panel.
        var samplerSteps = steps ?? DefaultSteps;
        var samplerCfg = cfg ?? DefaultCfg;

        // Output canvas size priority (img2img-friendly):
        //   1. User explicitly enabled Custom Resolution → use those exact dimensions
        //   2. Reference image present → derive dimensions from it (scaled to ~1MP, rounded
        //      to multiples of 16). Matches the official Klein workflows which use
        //      GetImageSize on the scaled reference, so an edit on a portrait doesn't get
        //      squashed to a square just because the aspect-ratio dropdown defaults to 1:1.
        //   3. Aspect-ratio dropdown set → use that
        //   4. Fall back to 1024x1024
        int outputWidth;
        int outputHeight;
        if (explicitDimensions && width.HasValue && height.HasValue)
        {
            outputWidth = width.Value;
            outputHeight = height.Value;
        }
        else if (TryGetReferenceImageDimensions(request) is (int refW, int refH))
        {
            (outputWidth, outputHeight) = ComputeTargetDimensions(
                refW,
                refH,
                TargetOutputMegapixels,
                ReferenceResolutionStep
            );
        }
        else if (width.HasValue && height.HasValue)
        {
            outputWidth = width.Value;
            outputHeight = height.Value;
        }
        else
        {
            outputWidth = DefaultWidth;
            outputHeight = DefaultHeight;
        }

        // 1. Load models — pass the user's UNET selection in so the model manager pairs
        // it with the matching qwen_3_4b or qwen_3_8b text encoder (Klein 9B and 4B use
        // different encoder sizes and ComfyUI rejects the workflow if they're mismatched).
        var modelManager = new Flux2KleinModelManager();
        var selectedModels = modelManager.SelectModels(clientManager, customUnetModel);

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

        // Single CLIPLoader with type="flux2" — Klein uses a single Qwen3 text encoder,
        // not the Flux.1-style dual CLIP-L + T5.
        var clipLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPLoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                ClipName = selectedModels.ClipModel.RelativePath,
                Type = DefaultClipType,
            }
        );

        var vaeLoader = nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = selectedModels.VaeModel.RelativePath,
            }
        );

        // Apply LoRAs if any
        var (currentModel, currentClip) = ComfyWorkflowHelper.ApplyLoras(
            nodes,
            loras,
            unetOutput,
            clipLoader.Output
        );

        // 2. Encode the positive prompt
        var positivePrompt = request.TextPrompt ?? "a beautiful image";

        var positiveTextEncode = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPTextEncode)),
                Clip = currentClip,
                Text = positivePrompt,
            }
        );

        // Negative = a SEPARATE empty-string CLIPTextEncode, NOT a zeroed positive.
        // This matches the official Comfy-Org Klein workflows for both distilled and base
        // variants. The distinction matters at CFG>1: ConditioningZeroOut produces a true
        // zero vector, so pred = 0 + cfg*(cond - 0) = cfg*cond, which blows out colors
        // ("deep-fried"). An empty-string encoding goes through the text encoder normally
        // and yields a small but non-zero baseline that the CFG math works against properly.
        var negativeTextEncode = nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = nodes.GetUniqueName("CLIPTextEncode_Negative"),
                Clip = currentClip,
                Text = string.Empty,
            }
        );

        // 3. Walk the reference images and chain them into both conditionings
        var positiveConditioning = positiveTextEncode.Output;
        var negativeConditioning = negativeTextEncode.Output;

        // Klein supports multi-reference editing; cap at 4 to keep prompt build time
        // and VRAM use predictable
        var referenceImageNames = ComfyWorkflowHelper.GetReferenceImageNames(
            request,
            maxImages: 4,
            providerPrefix: "flux2_klein"
        );
        for (var i = 0; i < referenceImageNames.Count; i++)
        {
            var imageName = referenceImageNames[i];
            var idx = i + 1;

            var loadImage = nodes.AddTypedNode(
                new ComfyNodeBuilder.LoadImage
                {
                    Name = nodes.GetUniqueName($"LoadImage_{idx}"),
                    Image = imageName,
                }
            );

            var scaledImage = nodes.AddTypedNode(
                new ComfyNodeBuilder.ImageScaleToTotalPixels
                {
                    Name = nodes.GetUniqueName($"ImageScaleToTotalPixels_{idx}"),
                    Image = loadImage.Output1,
                    UpscaleMethod = DefaultUpscaleMethod,
                    Megapixels = ReferenceMegapixels,
                    ResolutionSteps = ReferenceResolutionStep,
                }
            );

            var referenceLatent = nodes.AddTypedNode(
                new ComfyNodeBuilder.VAEEncode
                {
                    Name = nodes.GetUniqueName($"VAEEncode_Ref_{idx}"),
                    Pixels = scaledImage.Output,
                    Vae = vaeLoader.Output,
                }
            );

            var refOnPositive = nodes.AddTypedNode(
                new ComfyNodeBuilder.ReferenceLatent
                {
                    Name = nodes.GetUniqueName($"ReferenceLatent_Positive_{idx}"),
                    Conditioning = positiveConditioning,
                    Latent = referenceLatent.Output,
                }
            );
            positiveConditioning = refOnPositive.Output;

            var refOnNegative = nodes.AddTypedNode(
                new ComfyNodeBuilder.ReferenceLatent
                {
                    Name = nodes.GetUniqueName($"ReferenceLatent_Negative_{idx}"),
                    Conditioning = negativeConditioning,
                    Latent = referenceLatent.Output,
                }
            );
            negativeConditioning = refOnNegative.Output;
        }

        // 4. Empty Flux.2 latent for the output canvas
        var emptyLatent = nodes.AddTypedNode(
            new ComfyNodeBuilder.EmptyFlux2LatentImage
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.EmptyFlux2LatentImage)),
                Width = outputWidth,
                Height = outputHeight,
                BatchSize = 1,
            }
        );

        // 5. CFGGuider — distilled is trained for CFG=1, base variants for CFG=5
        var cfgGuider = nodes.AddTypedNode(
            new ComfyNodeBuilder.CFGGuider
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.CFGGuider)),
                Model = currentModel,
                Positive = positiveConditioning,
                Negative = negativeConditioning,
                Cfg = samplerCfg,
            }
        );

        // 6. Flux2Scheduler — 4 steps for distilled, ~20 for base variants
        var scheduler = nodes.AddTypedNode(
            new ComfyNodeBuilder.Flux2Scheduler
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.Flux2Scheduler)),
                Steps = samplerSteps,
                Width = outputWidth,
                Height = outputHeight,
            }
        );

        // 7. KSamplerSelect ("euler" per the official workflow)
        var samplerSelect = nodes.AddTypedNode(
            new ComfyNodeBuilder.KSamplerSelect
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.KSamplerSelect)),
                SamplerName = DefaultSampler,
            }
        );

        // 8. RandomNoise
        var randomNoise = nodes.AddTypedNode(
            new ComfyNodeBuilder.RandomNoise
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.RandomNoise)),
                NoiseSeed = seed,
            }
        );

        // 9. SamplerCustomAdvanced
        var sampler = nodes.AddTypedNode(
            new ComfyNodeBuilder.SamplerCustomAdvanced
            {
                Name = nodes.GetUniqueName(nameof(ComfyNodeBuilder.SamplerCustomAdvanced)),
                Noise = randomNoise.Output,
                Guider = cfgGuider.Output,
                Sampler = samplerSelect.Output,
                Sigmas = scheduler.Output,
                LatentImage = emptyLatent.Output,
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

        // 11. PreviewImage (output node — images retrieved from ComfyUI after execution)
        nodes.AddTypedNode(
            new ComfyNodeBuilder.PreviewImage
            {
                Name = nodes.GetUniqueName("SaveImage"),
                Images = vaeDecode.Output,
            }
        );

        return nodes;
    }

    /// <summary>
    /// Read the dimensions of the first available reference image (current input or, if
    /// none, the most recent assistant image in conversation history). Uses SKCodec which
    /// only parses the file header — no full pixel decode, so this is cheap even for big
    /// inputs.
    /// </summary>
    private static (int Width, int Height)? TryGetReferenceImageDimensions(ImageGenerationRequest request)
    {
        var base64 =
            request.InputImages?.FirstOrDefault()?.Base64Data
            ?? request
                .ConversationHistory?.LastOrDefault(m =>
                    m is { Role: MessageRole.Assistant, ImageContent: not null }
                )
                ?.ImageContent?.Base64Data;

        if (string.IsNullOrEmpty(base64))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            using var codec = SKCodec.Create(stream);
            if (codec == null)
                return null;
            return (codec.Info.Width, codec.Info.Height);
        }
        catch
        {
            // Malformed base64 / unsupported format — fall back to defaults
            return null;
        }
    }

    /// <summary>
    /// Scales (srcWidth, srcHeight) to approximately <paramref name="megapixels"/>
    /// preserving aspect ratio, with each dimension rounded down to the nearest multiple
    /// of <paramref name="step"/> (Flux.2 likes multiples of 16). Ensures at least one
    /// step on each axis.
    /// </summary>
    internal static (int Width, int Height) ComputeTargetDimensions(
        int srcWidth,
        int srcHeight,
        double megapixels,
        int step
    )
    {
        if (srcWidth <= 0 || srcHeight <= 0)
            return (DefaultWidth, DefaultHeight);

        var targetPixels = megapixels * 1_000_000.0;
        var scale = Math.Sqrt(targetPixels / (srcWidth * (double)srcHeight));
        var w = (int)Math.Round(srcWidth * scale);
        var h = (int)Math.Round(srcHeight * scale);

        // Snap to multiples of `step`
        w = Math.Max(step, (w / step) * step);
        h = Math.Max(step, (h / step) * step);

        return (w, h);
    }
}
