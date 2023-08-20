using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

/// <summary>
/// Builder functions for comfy nodes
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class ComfyNodeBuilder
{
    private readonly NodeDictionary nodes;

    public ComfyNodeBuilder(NodeDictionary nodes)
    {
        this.nodes = nodes;
    }
    
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
            Inputs = new Dictionary<string, object?> { ["pixels"] = pixels.Data, ["vae"] = vae.Data }
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
            Inputs = new Dictionary<string, object?> { ["latent"] = samples.Data, ["vae"] = vae.Data }
        };
    }
    
    public static NamedComfyNode<LatentNodeConnection> KSampler(
        string name,
        ModelNodeConnection model,
        ulong seed,
        int steps,
        double cfg,
        string samplerName,
        string scheduler,
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
                ["sampler_name"] = samplerName,
                ["scheduler"] = scheduler,
                ["positive"] = positive.Data,
                ["negative"] = negative.Data,
                ["latent_image"] = latentImage.Data,
                ["denoise"] = denoise
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
        string modelName)
    {
        return new NamedComfyNode<UpscaleModelNodeConnection>(name)
        {
            ClassType = "UpscaleModelLoader",
            Inputs = new Dictionary<string, object?> { ["model_name"] = modelName }
        };
    }

    public ImageNodeConnection Lambda_LatentToImage(LatentNodeConnection latent, VAENodeConnection vae)
    {
        return nodes.AddNamedNode(VAEDecode($"{GetRandomPrefix()}_VAEDecode", latent, vae)).Output;
    }
    
    public LatentNodeConnection Lambda_ImageToLatent(ImageNodeConnection pixels, VAENodeConnection vae)
    {
        return nodes.AddNamedNode(VAEEncode($"{GetRandomPrefix()}_VAEEncode", pixels, vae)).Output;
    }
    
    /// <summary>
    /// Create a upscaling node based on a <see cref="ComfyUpscalerType"/>
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_UpscaleWithModel(string name, string modelName, ImageNodeConnection image)
    {
        var modelLoader = nodes.AddNamedNode(
            UpscaleModelLoader($"{name}_UpscaleModelLoader", modelName));
        
        var upscaler = nodes.AddNamedNode(
            ImageUpscaleWithModel($"{name}_ImageUpscaleWithModel", modelLoader.Output, image));
        
        return upscaler;
    }
}
