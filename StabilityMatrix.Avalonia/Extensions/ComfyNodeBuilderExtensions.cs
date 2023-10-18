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

        builder.Connections.Primary = emptyLatent.Output;
        builder.Connections.PrimarySize = new Size(
            samplerCardViewModel.Width,
            samplerCardViewModel.Height
        );

        // If batch index is selected, add a LatentFromBatch
        if (batchSizeCardViewModel.IsBatchIndexEnabled)
        {
            builder.Connections.Primary = builder.Nodes
                .AddNamedNode(
                    ComfyNodeBuilder.LatentFromBatch(
                        "LatentFromBatch",
                        builder.GetPrimaryAsLatent(),
                        // remote expects a 0-based index, vm is 1-based
                        batchSizeCardViewModel.BatchIndex - 1,
                        1
                    )
                )
                .Output;
        }
    }

    public static void SetupBaseSampler(
        this ComfyNodeBuilder builder,
        SamplerCardViewModel samplerCardViewModel,
        PromptCardViewModel promptCardViewModel,
        ModelCardViewModel modelCardViewModel,
        IModelIndexService modelIndexService,
        Action<ComfyNodeBuilder>? postModelLoad = null
    )
    {
        // Load base checkpoint
        var checkpointLoader = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.CheckpointLoaderSimple(
                "CheckpointLoader",
                modelCardViewModel.SelectedModel?.FileName
                    ?? throw new NullReferenceException("Model not selected")
            )
        );

        builder.Connections.BaseModel = checkpointLoader.GetOutput<ModelNodeConnection>(0);
        builder.Connections.BaseClip = checkpointLoader.GetOutput<ClipNodeConnection>(1);
        builder.Connections.BaseVAE = checkpointLoader.GetOutput<VAENodeConnection>(2);
        builder.Connections.PrimaryVAE = builder.Connections.BaseVAE;

        // Run post model load action
        postModelLoad?.Invoke(builder);

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
                builder.Connections.BaseModel,
                builder.Connections.BaseClip,
                prompt.GetExtraNetworksAsLocalModels(modelIndexService)
            );

            // Set as source
            builder.Connections.BaseModel = lorasGroup.Output1;
            builder.Connections.BaseClip = lorasGroup.Output2;
        }

        // Clips
        var positiveClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode(
                "PositiveCLIP",
                builder.Connections.BaseClip,
                prompt.ProcessedText
            )
        );
        var negativeClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode(
                "NegativeCLIP",
                builder.Connections.BaseClip,
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
                    builder.Connections.BaseModel,
                    builder.Connections.Seed,
                    samplerCardViewModel.Steps,
                    samplerCardViewModel.CfgScale,
                    samplerCardViewModel.SelectedSampler
                        ?? throw new ValidationException("Sampler not selected"),
                    samplerCardViewModel.SelectedScheduler
                        ?? throw new ValidationException("Sampler not selected"),
                    positiveClip.Output,
                    negativeClip.Output,
                    builder.GetPrimaryAsLatent()
                        ?? throw new ValidationException("Latent source not set"),
                    samplerCardViewModel.DenoiseStrength
                )
            );
            builder.Connections.Primary = sampler.Output;
        }
        // Add base sampler (with refiner)
        else
        {
            // Total steps is the sum of the base and refiner steps
            var totalSteps = samplerCardViewModel.Steps + samplerCardViewModel.RefinerSteps;

            var sampler = builder.Nodes.AddNamedNode(
                ComfyNodeBuilder.KSamplerAdvanced(
                    "Sampler",
                    builder.Connections.BaseModel,
                    true,
                    builder.Connections.Seed,
                    totalSteps,
                    samplerCardViewModel.CfgScale,
                    samplerCardViewModel.SelectedSampler
                        ?? throw new ValidationException("Sampler not selected"),
                    samplerCardViewModel.SelectedScheduler
                        ?? throw new ValidationException("Sampler not selected"),
                    positiveClip.Output,
                    negativeClip.Output,
                    builder.GetPrimaryAsLatent(),
                    0,
                    samplerCardViewModel.Steps,
                    true
                )
            );
            builder.Connections.Primary = sampler.Output;
        }
    }

    public static void SetupRefinerSampler(
        this ComfyNodeBuilder builder,
        SamplerCardViewModel samplerCardViewModel,
        PromptCardViewModel promptCardViewModel,
        ModelCardViewModel modelCardViewModel,
        IModelIndexService modelIndexService,
        Action<ComfyNodeBuilder>? postModelLoad = null
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

        builder.Connections.RefinerModel = checkpointLoader.GetOutput<ModelNodeConnection>(0);
        builder.Connections.RefinerClip = checkpointLoader.GetOutput<ClipNodeConnection>(1);
        builder.Connections.RefinerVAE = checkpointLoader.GetOutput<VAENodeConnection>(2);
        builder.Connections.PrimaryVAE = builder.Connections.RefinerVAE;

        // Run post model load action
        postModelLoad?.Invoke(builder);

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
                builder.Connections.RefinerModel,
                builder.Connections.RefinerClip,
                prompt.GetExtraNetworksAsLocalModels(modelIndexService)
            );

            // Set as source
            builder.Connections.RefinerModel = lorasGroup.Output1;
            builder.Connections.RefinerClip = lorasGroup.Output2;
        }

        // Clips
        var positiveClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode(
                "Refiner_PositiveCLIP",
                builder.Connections.RefinerClip,
                prompt.ProcessedText
            )
        );
        var negativeClip = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ClipTextEncode(
                "Refiner_NegativeCLIP",
                builder.Connections.RefinerClip,
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
                builder.Connections.RefinerModel,
                false,
                builder.Connections.Seed,
                totalSteps,
                samplerCardViewModel.CfgScale,
                samplerCardViewModel.SelectedSampler
                    ?? throw new ValidationException("Sampler not selected"),
                samplerCardViewModel.SelectedScheduler
                    ?? throw new ValidationException("Sampler not selected"),
                positiveClip.Output,
                negativeClip.Output,
                builder.GetPrimaryAsLatent(),
                samplerCardViewModel.Steps,
                totalSteps,
                false
            )
        );

        builder.Connections.Primary = sampler.Output;
    }

    public static string SetupOutputImage(this ComfyNodeBuilder builder)
    {
        var previewImage = builder.Nodes.AddNamedNode(
            new NamedComfyNode("SaveImage")
            {
                ClassType = "PreviewImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["images"] = builder.GetPrimaryAsImage().Data
                }
            }
        );

        builder.Connections.OutputNodes.Add(previewImage);

        return previewImage.Name;
    }
}
