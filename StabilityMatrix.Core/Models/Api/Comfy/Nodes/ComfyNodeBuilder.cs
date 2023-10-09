using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Tokens;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

/// <summary>
/// Builder functions for comfy nodes
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class ComfyNodeBuilder
{
    public NodeDictionary Nodes { get; } = new();

    public Dictionary<Type, NodeConnectionBase> GlobalConnections { get; } = new();

    private static string GetRandomPrefix() => Guid.NewGuid().ToString()[..8];

    public static NamedComfyNode<LatentNodeConnection> VAEEncode(
        string name,
        ImageNodeConnection pixels,
        VAENodeConnection vae
    )
    {
        return new NamedComfyNode<LatentNodeConnection>(name)
        {
            ClassType = "VAEEncode",
            Inputs = new Dictionary<string, object?>
            {
                ["pixels"] = pixels.Data,
                ["vae"] = vae.Data
            }
        };
    }

    public static NamedComfyNode<ImageNodeConnection> VAEDecode(
        string name,
        LatentNodeConnection samples,
        VAENodeConnection vae
    )
    {
        return new NamedComfyNode<ImageNodeConnection>(name)
        {
            ClassType = "VAEDecode",
            Inputs = new Dictionary<string, object?>
            {
                ["samples"] = samples.Data,
                ["vae"] = vae.Data
            }
        };
    }

    public static NamedComfyNode<LatentNodeConnection> KSampler(
        string name,
        ModelNodeConnection model,
        ulong seed,
        int steps,
        double cfg,
        ComfySampler sampler,
        ComfyScheduler scheduler,
        ConditioningNodeConnection positive,
        ConditioningNodeConnection negative,
        LatentNodeConnection latentImage,
        double denoise
    )
    {
        return new NamedComfyNode<LatentNodeConnection>(name)
        {
            ClassType = "KSampler",
            Inputs = new Dictionary<string, object?>
            {
                ["model"] = model.Data,
                ["seed"] = seed,
                ["steps"] = steps,
                ["cfg"] = cfg,
                ["sampler_name"] = sampler.Name,
                ["scheduler"] = scheduler.Name,
                ["positive"] = positive.Data,
                ["negative"] = negative.Data,
                ["latent_image"] = latentImage.Data,
                ["denoise"] = denoise
            }
        };
    }

    public static NamedComfyNode<LatentNodeConnection> KSamplerAdvanced(
        string name,
        ModelNodeConnection model,
        bool addNoise,
        ulong noiseSeed,
        int steps,
        double cfg,
        ComfySampler sampler,
        ComfyScheduler scheduler,
        ConditioningNodeConnection positive,
        ConditioningNodeConnection negative,
        LatentNodeConnection latentImage,
        int startAtStep,
        int endAtStep,
        bool returnWithLeftoverNoise
    )
    {
        return new NamedComfyNode<LatentNodeConnection>(name)
        {
            ClassType = "KSamplerAdvanced",
            Inputs = new Dictionary<string, object?>
            {
                ["model"] = model.Data,
                ["add_noise"] = addNoise ? "enable" : "disable",
                ["noise_seed"] = noiseSeed,
                ["steps"] = steps,
                ["cfg"] = cfg,
                ["sampler_name"] = sampler.Name,
                ["scheduler"] = scheduler.Name,
                ["positive"] = positive.Data,
                ["negative"] = negative.Data,
                ["latent_image"] = latentImage.Data,
                ["start_at_step"] = startAtStep,
                ["end_at_step"] = endAtStep,
                ["return_with_leftover_noise"] = returnWithLeftoverNoise ? "enable" : "disable"
            }
        };
    }

    public static NamedComfyNode<LatentNodeConnection> EmptyLatentImage(
        string name,
        int batchSize,
        int height,
        int width
    )
    {
        return new NamedComfyNode<LatentNodeConnection>(name)
        {
            ClassType = "EmptyLatentImage",
            Inputs = new Dictionary<string, object?>
            {
                ["batch_size"] = batchSize,
                ["height"] = height,
                ["width"] = width,
            }
        };
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

    public static NamedComfyNode<UpscaleModelNodeConnection> UpscaleModelLoader(
        string name,
        string modelName
    )
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

    public static NamedComfyNode<VAENodeConnection> VAELoader(string name, string vaeModelName)
    {
        return new NamedComfyNode<VAENodeConnection>(name)
        {
            ClassType = "VAELoader",
            Inputs = new Dictionary<string, object?> { ["vae_name"] = vaeModelName }
        };
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

    public static NamedComfyNode<ModelNodeConnection> CheckpointLoaderSimple(
        string name,
        string modelName
    )
    {
        return new NamedComfyNode<ModelNodeConnection>(name)
        {
            ClassType = "CheckpointLoaderSimple",
            Inputs = new Dictionary<string, object?> { ["ckpt_name"] = modelName }
        };
    }

    public static NamedComfyNode<ModelNodeConnection> FreeU(
        string name,
        ModelNodeConnection model,
        double b1,
        double b2,
        double s1,
        double s2
    )
    {
        return new NamedComfyNode<ModelNodeConnection>(name)
        {
            ClassType = "FreeU",
            Inputs = new Dictionary<string, object?>
            {
                ["model"] = model.Data,
                ["b1"] = b1,
                ["b2"] = b2,
                ["s1"] = s1,
                ["s2"] = s2
            }
        };
    }

    public static NamedComfyNode<ConditioningNodeConnection> ClipTextEncode(
        string name,
        ClipNodeConnection clip,
        string text
    )
    {
        return new NamedComfyNode<ConditioningNodeConnection>(name)
        {
            ClassType = "CLIPTextEncode",
            Inputs = new Dictionary<string, object?> { ["clip"] = clip.Data, ["text"] = text }
        };
    }

    /// <summary>
    /// Create a LoadImage node.
    /// </summary>
    /// <param name="name">Name of the node</param>
    /// <param name="relativeInputPath">Path relative to the Comfy input directory</param>
    public static NamedComfyNode<ImageNodeConnection, ImageMaskConnection> LoadImage(
        string name,
        string relativeInputPath
    )
    {
        return new NamedComfyNode<ImageNodeConnection, ImageMaskConnection>(name)
        {
            ClassType = "LoadImage",
            Inputs = new Dictionary<string, object?> { ["image"] = relativeInputPath }
        };
    }

    public static NamedComfyNode<ImageNodeConnection> ImageSharpen(
        string name,
        ImageNodeConnection image,
        int sharpenRadius,
        double sigma,
        double alpha
    )
    {
        return new NamedComfyNode<ImageNodeConnection>(name)
        {
            ClassType = "ImageSharpen",
            Inputs = new Dictionary<string, object?>
            {
                ["image"] = image.Data,
                ["sharpen_radius"] = sharpenRadius,
                ["sigma"] = sigma,
                ["alpha"] = alpha
            }
        };
    }

    public ImageNodeConnection Lambda_LatentToImage(
        LatentNodeConnection latent,
        VAENodeConnection vae
    )
    {
        return Nodes.AddNamedNode(VAEDecode($"{GetRandomPrefix()}_VAEDecode", latent, vae)).Output;
    }

    public LatentNodeConnection Lambda_ImageToLatent(
        ImageNodeConnection pixels,
        VAENodeConnection vae
    )
    {
        return Nodes.AddNamedNode(VAEEncode($"{GetRandomPrefix()}_VAEEncode", pixels, vae)).Output;
    }

    /// <summary>
    /// Get a global connection for a given type
    /// </summary>
    public TConnection GetConnection<TConnection>()
        where TConnection : NodeConnectionBase
    {
        if (GlobalConnections.TryGetValue(typeof(TConnection), out var connection))
        {
            return (TConnection)connection;
        }

        throw new InvalidOperationException($"No global connection of type {typeof(TConnection)}");
    }

    /// <summary>
    /// Set a global connection for a given type
    /// </summary>
    public void SetConnection<TConnection>(TConnection connection)
        where TConnection : NodeConnectionBase
    {
        GlobalConnections[typeof(TConnection)] = connection;
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
        var modelLoader = Nodes.AddNamedNode(
            UpscaleModelLoader($"{name}_UpscaleModelLoader", modelName)
        );

        var upscaler = Nodes.AddNamedNode(
            ImageUpscaleWithModel($"{name}_ImageUpscaleWithModel", modelLoader.Output, image)
        );

        return upscaler;
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
            var samplerImage = Nodes.AddNamedNode(VAEDecode($"{name}_VAEDecode", latent, vae));

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale(
                    $"{name}_ImageScale",
                    modelUpscaler.Output,
                    "bilinear",
                    height,
                    width,
                    false
                )
            );

            // Convert back to latent space
            return Nodes.AddNamedNode(VAEEncode($"{name}_VAEEncode", resizedScaled.Output, vae));
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
            return Nodes.AddNamedNode(VAEDecode($"{name}_VAEDecode", latentUpscale.Output, vae));
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space
            var samplerImage = Nodes.AddNamedNode(VAEDecode($"{name}_VAEDecode", latent, vae));

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale(
                    $"{name}_ImageScale",
                    modelUpscaler.Output,
                    "bilinear",
                    height,
                    width,
                    false
                )
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
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                image
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale(
                    $"{name}_ImageScale",
                    modelUpscaler.Output,
                    "bilinear",
                    height,
                    width,
                    false
                )
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

        public ModelNodeConnection? BaseModel { get; set; }
        public VAENodeConnection? BaseVAE { get; set; }
        public ClipNodeConnection? BaseClip { get; set; }

        public ConditioningNodeConnection? BaseConditioning { get; set; }
        public ConditioningNodeConnection? BaseNegativeConditioning { get; set; }

        public ModelNodeConnection? RefinerModel { get; set; }
        public VAENodeConnection? RefinerVAE { get; set; }
        public ClipNodeConnection? RefinerClip { get; set; }
        public ConditioningNodeConnection? RefinerConditioning { get; set; }
        public ConditioningNodeConnection? RefinerNegativeConditioning { get; set; }

        public LatentNodeConnection? Latent { get; set; }
        public Size LatentSize { get; set; }

        public ImageNodeConnection? Image { get; set; }
        public Size ImageSize { get; set; }

        public List<NamedComfyNode> OutputNodes { get; } = new();

        public IEnumerable<string> OutputNodeNames => OutputNodes.Select(n => n.Name);

        /// <summary>
        /// Gets the latent size scaled by a given factor
        /// </summary>
        public Size GetScaledLatentSize(double scale)
        {
            return new Size(
                (int)Math.Floor(LatentSize.Width * scale),
                (int)Math.Floor(LatentSize.Height * scale)
            );
        }

        /// <summary>
        /// Gets the image size scaled by a given factor
        /// </summary>
        public Size GetScaledImageSize(double scale)
        {
            return new Size(
                (int)Math.Floor(ImageSize.Width * scale),
                (int)Math.Floor(ImageSize.Height * scale)
            );
        }

        public VAENodeConnection GetRefinerOrBaseVAE()
        {
            return RefinerVAE ?? BaseVAE ?? throw new NullReferenceException("No VAE");
        }

        public ModelNodeConnection GetRefinerOrBaseModel()
        {
            return RefinerModel ?? BaseModel ?? throw new NullReferenceException("No Model");
        }

        public ConditioningNodeConnection GetRefinerOrBaseConditioning()
        {
            return RefinerConditioning
                ?? BaseConditioning
                ?? throw new NullReferenceException("No Conditioning");
        }

        public ConditioningNodeConnection GetRefinerOrBaseNegativeConditioning()
        {
            return RefinerNegativeConditioning
                ?? BaseNegativeConditioning
                ?? throw new NullReferenceException("No Negative Conditioning");
        }
    }

    public NodeBuilderConnections Connections { get; } = new();
}
