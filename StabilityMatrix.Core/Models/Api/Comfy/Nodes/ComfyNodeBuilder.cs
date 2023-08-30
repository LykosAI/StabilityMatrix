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
            Inputs = new Dictionary<string, object?> { ["samples"] = samples.Data, ["vae"] = vae.Data }
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

    public static NamedComfyNode<ImageNodeConnection> ImageScale(
        string name,
        ImageNodeConnection image,
        string method,
        int height,
        int width,
        bool crop)
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
    
    public static NamedComfyNode<VAENodeConnection> VAELoader(
        string name,
        string vaeModelName)
    {
        return new NamedComfyNode<VAENodeConnection>(name)
        {
            ClassType = "VAELoader",
            Inputs = new Dictionary<string, object?>
            {
                ["vae_name"] = vaeModelName
            }
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
    /// Create a group node that upscales a given image with a given model
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_UpscaleWithModel(string name, string modelName, ImageNodeConnection image)
    {
        var modelLoader = nodes.AddNamedNode(
            UpscaleModelLoader($"{name}_UpscaleModelLoader", modelName));
        
        var upscaler = nodes.AddNamedNode(
            ImageUpscaleWithModel($"{name}_ImageUpscaleWithModel", modelLoader.Output, image));
        
        return upscaler;
    }

    /// <summary>
    /// Create a group node that scales a given image to a given size
    /// </summary>
    public NamedComfyNode<LatentNodeConnection> Group_UpscaleToLatent(string name,
        LatentNodeConnection latent, VAENodeConnection vae,
        ComfyUpscaler upscaleInfo, int width, int height)
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            return nodes
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
                );
        }
        
        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space
            var samplerImage = nodes.AddNamedNode(
                VAEDecode(
                    $"{name}_VAEDecode",
                    latent,
                    vae
                )
            );

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = nodes.AddNamedNode(
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
            return nodes
                .AddNamedNode(
                    VAEEncode(
                        $"{name}_VAEEncode",
                        resizedScaled.Output,
                        vae
                    )
                );
        }
        
        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }
    
    /// <summary>
    /// Create a group node that scales a given image to image output
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_UpscaleToImage(string name,
        LatentNodeConnection latent, VAENodeConnection vae,
        ComfyUpscaler upscaleInfo, int width, int height)
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            var latentUpscale = nodes
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
                );
            
            // Convert to image space
            return nodes.AddNamedNode(
                VAEDecode(
                    $"{name}_VAEDecode",
                    latentUpscale.Output,
                    vae
                )
            );
        }
        
        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space
            var samplerImage = nodes.AddNamedNode(
                VAEDecode(
                    $"{name}_VAEDecode",
                    latent,
                    vae
                )
            );

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = nodes.AddNamedNode(
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
}
