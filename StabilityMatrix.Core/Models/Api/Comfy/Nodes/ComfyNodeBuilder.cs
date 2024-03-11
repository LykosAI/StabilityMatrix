using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using OneOf;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

/// <summary>
/// Builder functions for comfy nodes
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[Localizable(false)]
public class ComfyNodeBuilder
{
    public NodeDictionary Nodes { get; } = new();

    private static string GetRandomPrefix() => Guid.NewGuid().ToString()[..8];

    private string GetUniqueName(string nameBase)
    {
        var name = $"{nameBase}_1";
        for (var i = 0; Nodes.ContainsKey(name); i++)
        {
            if (i > 1_000_000)
            {
                throw new InvalidOperationException($"Could not find unique name for base {nameBase}");
            }

            name = $"{nameBase}_{i + 1}";
        }

        return name;
    }

    public record VAEEncode : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ImageNodeConnection Pixels { get; init; }
        public required VAENodeConnection Vae { get; init; }
    }

    public record VAEDecode : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required LatentNodeConnection Samples { get; init; }
        public required VAENodeConnection Vae { get; init; }
    }

    public record KSampler : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required ulong Seed { get; init; }
        public required int Steps { get; init; }
        public required double Cfg { get; init; }
        public required string SamplerName { get; init; }
        public required string Scheduler { get; init; }
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
        public required double Denoise { get; init; }
    }

    public record KSamplerAdvanced : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        [BoolStringMember("enable", "disable")]
        public required bool AddNoise { get; init; }
        public required ulong NoiseSeed { get; init; }
        public required int Steps { get; init; }
        public required double Cfg { get; init; }
        public required string SamplerName { get; init; }
        public required string Scheduler { get; init; }
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
        public required int StartAtStep { get; init; }
        public required int EndAtStep { get; init; }

        [BoolStringMember("enable", "disable")]
        public bool ReturnWithLeftoverNoise { get; init; }
    }

    public record SamplerCustom : ComfyTypedNodeBase<LatentNodeConnection, LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required bool AddNoise { get; init; }
        public required ulong NoiseSeed { get; init; }

        [Range(0d, 100d)]
        public required double Cfg { get; init; }

        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required SamplerNodeConnection Sampler { get; init; }
        public required SigmasNodeConnection Sigmas { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
    }

    public record KSamplerSelect : ComfyTypedNodeBase<SamplerNodeConnection>
    {
        public required string SamplerName { get; init; }
    }

    public record SDTurboScheduler : ComfyTypedNodeBase<SigmasNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        [Range(1, 10)]
        public required int Steps { get; init; }

        [Range(0, 1.0)]
        public required double Denoise { get; init; }
    }

    public record EmptyLatentImage : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required int BatchSize { get; init; }
        public required int Height { get; init; }
        public required int Width { get; init; }
    }

    public record CLIPSetLastLayer : ComfyTypedNodeBase<ClipNodeConnection>
    {
        public required ClipNodeConnection Clip { get; init; }

        [Range(-24, -1)]
        public int StopAtClipLayer { get; init; } = -1;
    }

    public static NamedComfyNode<LatentNodeConnection> LatentFromBatch(
        string name,
        LatentNodeConnection samples,
        int batchIndex,
        int length
    )
    {
        return new NamedComfyNode<LatentNodeConnection>(name)
        {
            ClassType = "LatentFromBatch",
            Inputs = new Dictionary<string, object?>
            {
                ["samples"] = samples.Data,
                ["batch_index"] = batchIndex,
                ["length"] = length,
            }
        };
    }

    public static NamedComfyNode<ImageNodeConnection> ImageUpscaleWithModel(
        string name,
        UpscaleModelNodeConnection upscaleModel,
        ImageNodeConnection image
    )
    {
        return new NamedComfyNode<ImageNodeConnection>(name)
        {
            ClassType = "ImageUpscaleWithModel",
            Inputs = new Dictionary<string, object?>
            {
                ["upscale_model"] = upscaleModel.Data,
                ["image"] = image.Data
            }
        };
    }

    public static NamedComfyNode<UpscaleModelNodeConnection> UpscaleModelLoader(string name, string modelName)
    {
        return new NamedComfyNode<UpscaleModelNodeConnection>(name)
        {
            ClassType = "UpscaleModelLoader",
            Inputs = new Dictionary<string, object?> { ["model_name"] = modelName }
        };
    }

    public static NamedComfyNode<ImageNodeConnection> ImageScale(
        string name,
        ImageNodeConnection image,
        string method,
        int height,
        int width,
        bool crop
    )
    {
        return new NamedComfyNode<ImageNodeConnection>(name)
        {
            ClassType = "ImageScale",
            Inputs = new Dictionary<string, object?>
            {
                ["image"] = image.Data,
                ["upscale_method"] = method,
                ["height"] = height,
                ["width"] = width,
                ["crop"] = crop ? "center" : "disabled"
            }
        };
    }

    public record VAELoader : ComfyTypedNodeBase<VAENodeConnection>
    {
        public required string VaeName { get; init; }
    }

    public static NamedComfyNode<ModelNodeConnection, ClipNodeConnection> LoraLoader(
        string name,
        ModelNodeConnection model,
        ClipNodeConnection clip,
        string loraName,
        double strengthModel,
        double strengthClip
    )
    {
        return new NamedComfyNode<ModelNodeConnection, ClipNodeConnection>(name)
        {
            ClassType = "LoraLoader",
            Inputs = new Dictionary<string, object?>
            {
                ["model"] = model.Data,
                ["clip"] = clip.Data,
                ["lora_name"] = loraName,
                ["strength_model"] = strengthModel,
                ["strength_clip"] = strengthClip
            }
        };
    }

    public record CheckpointLoader
        : ComfyTypedNodeBase<ModelNodeConnection, ClipNodeConnection, VAENodeConnection>
    {
        public required string ConfigName { get; init; }
        public required string CkptName { get; init; }
    }

    public record CheckpointLoaderSimple
        : ComfyTypedNodeBase<ModelNodeConnection, ClipNodeConnection, VAENodeConnection>
    {
        public required string CkptName { get; init; }
    }

    public record ImageOnlyCheckpointLoader
        : ComfyTypedNodeBase<ModelNodeConnection, ClipVisionNodeConnection, VAENodeConnection>
    {
        public required string CkptName { get; init; }
    }

    public record FreeU : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required double B1 { get; init; }
        public required double B2 { get; init; }
        public required double S1 { get; init; }
        public required double S2 { get; init; }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public record CLIPTextEncode : ComfyTypedNodeBase<ConditioningNodeConnection>
    {
        public required ClipNodeConnection Clip { get; init; }
        public required OneOf<string, StringNodeConnection> Text { get; init; }
    }

    public record LoadImage : ComfyTypedNodeBase<ImageNodeConnection, ImageMaskConnection>
    {
        /// <summary>
        /// Path relative to the Comfy input directory
        /// </summary>
        public required string Image { get; init; }
    }

    public record PreviewImage : ComfyTypedNodeBase
    {
        public required ImageNodeConnection Images { get; init; }
    }

    public record ImageSharpen : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required ImageNodeConnection Image { get; init; }
        public required int SharpenRadius { get; init; }
        public required double Sigma { get; init; }
        public required double Alpha { get; init; }
    }

    public record ControlNetLoader : ComfyTypedNodeBase<ControlNetNodeConnection>
    {
        public required string ControlNetName { get; init; }
    }

    public record ControlNetApplyAdvanced
        : ComfyTypedNodeBase<ConditioningNodeConnection, ConditioningNodeConnection>
    {
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required ControlNetNodeConnection ControlNet { get; init; }
        public required ImageNodeConnection Image { get; init; }
        public required double Strength { get; init; }
        public required double StartPercent { get; init; }
        public required double EndPercent { get; init; }
    }

    public record SVD_img2vid_Conditioning
        : ComfyTypedNodeBase<ConditioningNodeConnection, ConditioningNodeConnection, LatentNodeConnection>
    {
        public required ClipVisionNodeConnection ClipVision { get; init; }
        public required ImageNodeConnection InitImage { get; init; }
        public required VAENodeConnection Vae { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int VideoFrames { get; init; }
        public required int MotionBucketId { get; init; }
        public required int Fps { get; set; }
        public required double AugmentationLevel { get; init; }
    }

    public record VideoLinearCFGGuidance : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required double MinCfg { get; init; }
    }

    public record SaveAnimatedWEBP : ComfyTypedNodeBase
    {
        public required ImageNodeConnection Images { get; init; }
        public required string FilenamePrefix { get; init; }
        public required double Fps { get; init; }
        public required bool Lossless { get; init; }
        public required int Quality { get; init; }
        public required string Method { get; init; }
    }

    [TypedNodeOptions(
        Name = "Inference_Core_PromptExpansion",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes"]
    )]
    public record PromptExpansion : ComfyTypedNodeBase<StringNodeConnection>
    {
        public required string ModelName { get; init; }
        public required OneOf<string, StringNodeConnection> Text { get; init; }
        public required ulong Seed { get; init; }
        public bool LogPrompt { get; init; }
    }

    [TypedNodeOptions(
        Name = "Inference_Core_AIO_Preprocessor",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes >= 0.2.0"]
    )]
    public record AIOPreprocessor : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required ImageNodeConnection Image { get; init; }

        public required string Preprocessor { get; init; }

        [Range(64, 2048)]
        public int Resolution { get; init; } = 512;
    }

    [TypedNodeOptions(
        Name = "Inference_Core_ReferenceOnlySimple",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes >= 0.3.0"]
    )]
    public record ReferenceOnlySimple : ComfyTypedNodeBase<ModelNodeConnection, LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        public required LatentNodeConnection Reference { get; init; }

        [Range(1, 64)]
        public int BatchSize { get; init; } = 1;
    }

    public ImageNodeConnection Lambda_LatentToImage(LatentNodeConnection latent, VAENodeConnection vae)
    {
        var name = GetUniqueName("VAEDecode");
        return Nodes
            .AddTypedNode(
                new VAEDecode
                {
                    Name = name,
                    Samples = latent,
                    Vae = vae
                }
            )
            .Output;
    }

    public LatentNodeConnection Lambda_ImageToLatent(ImageNodeConnection pixels, VAENodeConnection vae)
    {
        var name = GetUniqueName("VAEEncode");
        return Nodes
            .AddTypedNode(
                new VAEEncode
                {
                    Name = name,
                    Pixels = pixels,
                    Vae = vae
                }
            )
            .Output;
    }

    /// <summary>
    /// Create a group node that upscales a given image with a given model
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_UpscaleWithModel(
        string name,
        string modelName,
        ImageNodeConnection image
    )
    {
        var modelLoader = Nodes.AddNamedNode(UpscaleModelLoader($"{name}_UpscaleModelLoader", modelName));

        var upscaler = Nodes.AddNamedNode(
            ImageUpscaleWithModel($"{name}_ImageUpscaleWithModel", modelLoader.Output, image)
        );

        return upscaler;
    }

    /// <summary>
    /// Create a group node that scales a given image to image output
    /// </summary>
    public PrimaryNodeConnection Group_Upscale(
        string name,
        PrimaryNodeConnection primary,
        VAENodeConnection vae,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            return primary.Match<PrimaryNodeConnection>(
                latent =>
                    Nodes
                        .AddNamedNode(
                            new NamedComfyNode<LatentNodeConnection>($"{name}_LatentUpscale")
                            {
                                ClassType = "LatentUpscale",
                                Inputs = new Dictionary<string, object?>
                                {
                                    ["upscale_method"] = upscaleInfo.Name,
                                    ["width"] = width,
                                    ["height"] = height,
                                    ["crop"] = "disabled",
                                    ["samples"] = latent.Data,
                                }
                            }
                        )
                        .Output,
                image =>
                    Nodes
                        .AddNamedNode(
                            ImageScale($"{name}_ImageUpscale", image, upscaleInfo.Name, height, width, false)
                        )
                        .Output
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space if needed
            var samplerImage = GetPrimaryAsImage(primary, vae);

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            return resizedScaled.Output;
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that scales a given image to a given size
    /// </summary>
    public NamedComfyNode<LatentNodeConnection> Group_UpscaleToLatent(
        string name,
        LatentNodeConnection latent,
        VAENodeConnection vae,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            return Nodes.AddNamedNode(
                new NamedComfyNode<LatentNodeConnection>($"{name}_LatentUpscale")
                {
                    ClassType = "LatentUpscale",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["upscale_method"] = upscaleInfo.Name,
                        ["width"] = width,
                        ["height"] = height,
                        ["crop"] = "disabled",
                        ["samples"] = latent.Data,
                    }
                }
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space
            var samplerImage = Nodes.AddTypedNode(
                new VAEDecode
                {
                    Name = $"{name}_VAEDecode",
                    Samples = latent,
                    Vae = vae
                }
            );

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            // Convert back to latent space
            return Nodes.AddTypedNode(
                new VAEEncode
                {
                    Name = $"{name}_VAEEncode",
                    Pixels = resizedScaled.Output,
                    Vae = vae
                }
            );
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that scales a given image to image output
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_LatentUpscaleToImage(
        string name,
        LatentNodeConnection latent,
        VAENodeConnection vae,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            var latentUpscale = Nodes.AddNamedNode(
                new NamedComfyNode<LatentNodeConnection>($"{name}_LatentUpscale")
                {
                    ClassType = "LatentUpscale",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["upscale_method"] = upscaleInfo.Name,
                        ["width"] = width,
                        ["height"] = height,
                        ["crop"] = "disabled",
                        ["samples"] = latent.Data,
                    }
                }
            );

            // Convert to image space
            return Nodes.AddTypedNode(
                new VAEDecode
                {
                    Name = $"{name}_VAEDecode",
                    Samples = latentUpscale.Output,
                    Vae = vae
                }
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space
            var samplerImage = Nodes.AddTypedNode(
                new VAEDecode
                {
                    Name = $"{name}_VAEDecode",
                    Samples = latent,
                    Vae = vae
                }
            );

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            // No need to convert back to latent space
            return resizedScaled;
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that scales a given image to image output
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_UpscaleToImage(
        string name,
        ImageNodeConnection image,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            return Nodes.AddNamedNode(
                new NamedComfyNode<ImageNodeConnection>($"{name}_LatentUpscale")
                {
                    ClassType = "ImageScale",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["image"] = image,
                        ["upscale_method"] = upscaleInfo.Name,
                        ["width"] = width,
                        ["height"] = height,
                        ["crop"] = "disabled",
                    }
                }
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel($"{name}_ModelUpscale", upscaleInfo.Name, image);

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            // No need to convert back to latent space
            return resizedScaled;
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that loads multiple Lora's in series
    /// </summary>
    public NamedComfyNode<ModelNodeConnection, ClipNodeConnection> Group_LoraLoadMany(
        string name,
        ModelNodeConnection model,
        ClipNodeConnection clip,
        IEnumerable<(string FileName, double? ModelWeight, double? ClipWeight)> loras
    )
    {
        NamedComfyNode<ModelNodeConnection, ClipNodeConnection>? currentNode = null;

        foreach (var (i, loraNetwork) in loras.Enumerate())
        {
            currentNode = Nodes.AddNamedNode(
                LoraLoader(
                    $"{name}_LoraLoader_{i + 1}",
                    model,
                    clip,
                    loraNetwork.FileName,
                    loraNetwork.ModelWeight ?? 1,
                    loraNetwork.ClipWeight ?? 1
                )
            );

            // Connect to previous node
            model = currentNode.Output1;
            clip = currentNode.Output2;
        }

        return currentNode ?? throw new InvalidOperationException("No lora networks given");
    }

    /// <summary>
    /// Create a group node that loads multiple Lora's in series
    /// </summary>
    public NamedComfyNode<ModelNodeConnection, ClipNodeConnection> Group_LoraLoadMany(
        string name,
        ModelNodeConnection model,
        ClipNodeConnection clip,
        IEnumerable<(LocalModelFile ModelFile, double? ModelWeight, double? ClipWeight)> loras
    )
    {
        NamedComfyNode<ModelNodeConnection, ClipNodeConnection>? currentNode = null;

        foreach (var (i, loraNetwork) in loras.Enumerate())
        {
            currentNode = Nodes.AddNamedNode(
                LoraLoader(
                    $"{name}_LoraLoader_{i + 1}",
                    model,
                    clip,
                    loraNetwork.ModelFile.RelativePathFromSharedFolder,
                    loraNetwork.ModelWeight ?? 1,
                    loraNetwork.ClipWeight ?? 1
                )
            );

            // Connect to previous node
            model = currentNode.Output1;
            clip = currentNode.Output2;
        }

        return currentNode ?? throw new InvalidOperationException("No lora networks given");
    }

    /// <summary>
    /// Get or convert latest primary connection to latent
    /// </summary>
    public LatentNodeConnection GetPrimaryAsLatent()
    {
        if (Connections.Primary?.IsT0 == true)
        {
            return Connections.Primary.AsT0;
        }

        return GetPrimaryAsLatent(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            Connections.GetDefaultVAE()
        );
    }

    /// <summary>
    /// Get or convert latest primary connection to latent
    /// </summary>
    public LatentNodeConnection GetPrimaryAsLatent(PrimaryNodeConnection primary, VAENodeConnection vae)
    {
        return primary.Match(latent => latent, image => Lambda_ImageToLatent(image, vae));
    }

    /// <summary>
    /// Get or convert latest primary connection to latent
    /// </summary>
    public LatentNodeConnection GetPrimaryAsLatent(VAENodeConnection vae)
    {
        if (Connections.Primary?.IsT0 == true)
        {
            return Connections.Primary.AsT0;
        }

        return GetPrimaryAsLatent(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            vae
        );
    }

    /// <summary>
    /// Get or convert latest primary connection to image
    /// </summary>
    public ImageNodeConnection GetPrimaryAsImage()
    {
        if (Connections.Primary?.IsT1 == true)
        {
            return Connections.Primary.AsT1;
        }

        return GetPrimaryAsImage(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            Connections.GetDefaultVAE()
        );
    }

    /// <summary>
    /// Get or convert latest primary connection to image
    /// </summary>
    public ImageNodeConnection GetPrimaryAsImage(PrimaryNodeConnection primary, VAENodeConnection vae)
    {
        return primary.Match(latent => Lambda_LatentToImage(latent, vae), image => image);
    }

    /// <summary>
    /// Get or convert latest primary connection to image
    /// </summary>
    public ImageNodeConnection GetPrimaryAsImage(VAENodeConnection vae)
    {
        if (Connections.Primary?.IsT1 == true)
        {
            return Connections.Primary.AsT1;
        }

        return GetPrimaryAsImage(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            vae
        );
    }

    /// <summary>
    /// Convert to a NodeDictionary
    /// </summary>
    public NodeDictionary ToNodeDictionary()
    {
        Nodes.NormalizeConnectionTypes();
        return Nodes;
    }

    public class NodeBuilderConnections
    {
        public ulong Seed { get; set; }

        public int BatchSize { get; set; } = 1;
        public int? BatchIndex { get; set; }

        public OneOf<string, StringNodeConnection> PositivePrompt { get; set; }
        public OneOf<string, StringNodeConnection> NegativePrompt { get; set; }

        public ClipNodeConnection? BaseClip { get; set; }
        public ClipVisionNodeConnection? BaseClipVision { get; set; }

        public Dictionary<string, ModelConnections> Models { get; } =
            new() { ["Base"] = new ModelConnections("Base"), ["Refiner"] = new ModelConnections("Refiner") };

        /// <summary>
        /// ModelConnections from <see cref="Models"/> with <see cref="ModelConnections.Model"/> set
        /// </summary>
        public IEnumerable<ModelConnections> LoadedModels => Models.Values.Where(m => m.Model is not null);

        public ModelConnections Base => Models["Base"];
        public ModelConnections Refiner => Models["Refiner"];

        public Dictionary<string, ModuleApplyStepTemporaryArgs?> SamplerTemporaryArgs { get; } = new();

        public ModuleApplyStepTemporaryArgs? BaseSamplerTemporaryArgs
        {
            get => SamplerTemporaryArgs.GetValueOrDefault("Base");
            set => SamplerTemporaryArgs["Base"] = value;
        }

        public PrimaryNodeConnection? Primary { get; set; }
        public VAENodeConnection? PrimaryVAE { get; set; }
        public Size PrimarySize { get; set; }

        public ComfySampler? PrimarySampler { get; set; }
        public ComfyScheduler? PrimaryScheduler { get; set; }

        public List<NamedComfyNode> OutputNodes { get; } = new();

        public IEnumerable<string> OutputNodeNames => OutputNodes.Select(n => n.Name);

        public ModelNodeConnection GetRefinerOrBaseModel()
        {
            return Refiner.Model
                ?? Base.Model
                ?? throw new NullReferenceException("No Refiner or Base Model");
        }

        public ConditioningConnections GetRefinerOrBaseConditioning()
        {
            return Refiner.Conditioning
                ?? Base.Conditioning
                ?? throw new NullReferenceException("No Refiner or Base Conditioning");
        }

        public VAENodeConnection GetDefaultVAE()
        {
            return PrimaryVAE ?? Refiner.VAE ?? Base.VAE ?? throw new NullReferenceException("No VAE");
        }
    }

    public NodeBuilderConnections Connections { get; } = new();
}
