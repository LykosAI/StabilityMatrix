using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Extensions;

public static class ComfyNodeBuilderExtensions
{
    public static void SetupLatentSource(
        this ComfyNodeBuilder builder,
        BatchSizeCardViewModel batchSizeCardViewModel,
        SamplerCardViewModel samplerCardViewModel
    )
    {
        var emptyLatent = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.EmptyLatentImage(
                "EmptyLatentImage",
                batchSizeCardViewModel.BatchSize,
                samplerCardViewModel.Height,
                samplerCardViewModel.Width
            )
        );

        builder.Connections.Latent = emptyLatent.Output;
        builder.Connections.LatentSize = new Size(
            samplerCardViewModel.Width,
            samplerCardViewModel.Height
        );
    }

    public static void SetupBaseSampler(
        this ComfyNodeBuilder builder,
        SeedCardViewModel seedCardViewModel,
        SamplerCardViewModel samplerCardViewModel,
        PromptCardViewModel promptCardViewModel,
        ModelCardViewModel modelCardViewModel,
        IModelIndexService modelIndexService
    )
    {
        // Load base checkpoint
        var checkpointLoader = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.CheckpointLoaderSimple(
                "CheckpointLoader",
                modelCardViewModel.SelectedModelName
                    ?? throw new NullReferenceException("Model not selected")
            )
        );

        builder.Connections.BaseVAE = checkpointLoader.GetOutput<VAENodeConnection>(2);

        // Define model and clip for connections for chaining
        var modelSource = checkpointLoader.GetOutput<ModelNodeConnection>(0);
        var clipSource = checkpointLoader.GetOutput<ClipNodeConnection>(1);

        // Load prompts
        var prompt = promptCardViewModel.GetPrompt();
        prompt.Process();
        var negativePrompt = promptCardViewModel.GetNegativePrompt();
        negativePrompt.Process();

        // If need to load loras, add a group
        if (prompt.ExtraNetworks.Count > 0)
        {
            // Convert to local file names
            var lorasGroup = builder.Group_LoraLoadMany(
                "Loras",
                modelSource,
                clipSource,
                prompt.GetExtraNetworksAsLocalModels(modelIndexService)
            );

            // Set as source
            modelSource = lorasGroup.Output1;
            clipSource = lorasGroup.Output2;
        }
        builder.Connections.BaseModel = modelSource;

        // Clips
        var positiveClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode("PositiveCLIP", clipSource, prompt.ProcessedText)
        );
        var negativeClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode(
                "NegativeCLIP",
                clipSource,
                negativePrompt.ProcessedText
            )
        );
        builder.Connections.BaseConditioning = positiveClip.Output;
        builder.Connections.BaseNegativeConditioning = negativeClip.Output;

        // Add base sampler (without refiner)
        if (
            modelCardViewModel
            is not { IsRefinerSelectionEnabled: true, SelectedRefiner.IsDefault: false }
        )
        {
            var sampler = builder.Nodes.AddNamedNode(
                ComfyNodeBuilder.KSampler(
                    "Sampler",
                    modelSource,
                    Convert.ToUInt64(seedCardViewModel.Seed),
                    samplerCardViewModel.Steps,
                    samplerCardViewModel.CfgScale,
                    samplerCardViewModel.SelectedSampler
                        ?? throw new ValidationException("Sampler not selected"),
                    samplerCardViewModel.SelectedScheduler
                        ?? throw new ValidationException("Sampler not selected"),
                    positiveClip.Output,
                    negativeClip.Output,
                    builder.Connections.Latent
                        ?? throw new ValidationException("Latent source not set"),
                    samplerCardViewModel.DenoiseStrength
                )
            );
            builder.Connections.Latent = sampler.Output;
        }
        // Add base sampler (with refiner)
        else
        {
            // Total steps is the sum of the base and refiner steps
            var totalSteps = samplerCardViewModel.Steps + samplerCardViewModel.RefinerSteps;

            var sampler = builder.Nodes.AddNamedNode(
                ComfyNodeBuilder.KSamplerAdvanced(
                    "Sampler",
                    modelSource,
                    true,
                    Convert.ToUInt64(seedCardViewModel.Seed),
                    totalSteps,
                    samplerCardViewModel.CfgScale,
                    samplerCardViewModel.SelectedSampler
                        ?? throw new ValidationException("Sampler not selected"),
                    samplerCardViewModel.SelectedScheduler
                        ?? throw new ValidationException("Sampler not selected"),
                    positiveClip.Output,
                    negativeClip.Output,
                    builder.Connections.Latent
                        ?? throw new ValidationException("Latent source not set"),
                    0,
                    samplerCardViewModel.Steps,
                    true
                )
            );
            builder.Connections.Latent = sampler.Output;
        }
    }

    public static void SetupRefinerSampler(
        this ComfyNodeBuilder builder,
        SeedCardViewModel seedCardViewModel,
        SamplerCardViewModel samplerCardViewModel,
        PromptCardViewModel promptCardViewModel,
        ModelCardViewModel modelCardViewModel,
        IModelIndexService modelIndexService
    )
    {
        // Load refiner checkpoint
        var checkpointLoader = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.CheckpointLoaderSimple(
                "Refiner_CheckpointLoader",
                modelCardViewModel.SelectedRefiner?.FileName
                    ?? throw new NullReferenceException("Model not selected")
            )
        );

        builder.Connections.RefinerVAE = checkpointLoader.GetOutput<VAENodeConnection>(2);

        // Define model and clip for connections for chaining
        var modelSource = checkpointLoader.GetOutput<ModelNodeConnection>(0);
        var clipSource = checkpointLoader.GetOutput<ClipNodeConnection>(1);

        // Load prompts
        var prompt = promptCardViewModel.GetPrompt();
        prompt.Process();
        var negativePrompt = promptCardViewModel.GetNegativePrompt();
        negativePrompt.Process();

        // If need to load loras, add a group
        if (prompt.ExtraNetworks.Count > 0)
        {
            // Convert to local file names
            var lorasGroup = builder.Group_LoraLoadMany(
                "Refiner_Loras",
                modelSource,
                clipSource,
                prompt.GetExtraNetworksAsLocalModels(modelIndexService)
            );

            // Set as source
            modelSource = lorasGroup.Output1;
            clipSource = lorasGroup.Output2;
        }
        builder.Connections.RefinerModel = modelSource;

        // Clips
        var positiveClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode(
                "Refiner_PositiveCLIP",
                clipSource,
                prompt.ProcessedText
            )
        );
        var negativeClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode(
                "Refiner_NegativeCLIP",
                clipSource,
                negativePrompt.ProcessedText
            )
        );
        builder.Connections.RefinerConditioning = positiveClip.Output;
        builder.Connections.RefinerNegativeConditioning = negativeClip.Output;

        // Add refiner sampler

        // Total steps is the sum of the base and refiner steps
        var totalSteps = samplerCardViewModel.Steps + samplerCardViewModel.RefinerSteps;

        var sampler = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.KSamplerAdvanced(
                "Refiner_Sampler",
                modelSource,
                false,
                Convert.ToUInt64(seedCardViewModel.Seed),
                totalSteps,
                samplerCardViewModel.CfgScale,
                samplerCardViewModel.SelectedSampler
                    ?? throw new ValidationException("Sampler not selected"),
                samplerCardViewModel.SelectedScheduler
                    ?? throw new ValidationException("Sampler not selected"),
                positiveClip.Output,
                negativeClip.Output,
                builder.Connections.Latent
                    ?? throw new ValidationException("Latent source not set"),
                samplerCardViewModel.Steps,
                totalSteps,
                false
            )
        );
        builder.Connections.Latent = sampler.Output;
    }

    public static string SetupOutputImage(this ComfyNodeBuilder builder)
    {
        // Do VAE decoding if not done already
        if (builder.Connections.Image is null)
        {
            var vaeDecoder = builder.Nodes.AddNamedNode(
                ComfyNodeBuilder.VAEDecode(
                    "VAEDecode",
                    builder.Connections.Latent!,
                    builder.Connections.GetRefinerOrBaseVAE()
                )
            );
            builder.Connections.Image = vaeDecoder.Output;
        }

        var saveImage = builder.Nodes.AddNamedNode(
            new NamedComfyNode("SaveImage")
            {
                ClassType = "SaveImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["filename_prefix"] = "Inference/TextToImage",
                    ["images"] = builder.Connections.Image
                }
            }
        );

        return saveImage.Name;
    }
}
