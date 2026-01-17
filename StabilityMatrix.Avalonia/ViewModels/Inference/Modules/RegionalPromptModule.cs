using System.Collections.Generic;
using System.IO;
using Injectio.Attributes;
using SkiaSharp;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

/// <summary>
/// Module for regional prompting - apply different prompts to different regions of the image.
/// Uses layers with painted masks to define regions.
/// </summary>
[ManagedService]
[RegisterTransient<RegionalPromptModule>]
public class RegionalPromptModule : ModuleBase
{
    private int maskCounter;

    /// <inheritdoc />
    public RegionalPromptModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Regional Prompting";
        AddCards(vmFactory.Get<RegionalPromptCardViewModel>());
    }

    /// <inheritdoc />
    protected override IEnumerable<ImageSource> GetInputImages()
    {
        // Regional prompting masks are transferred via FilesToTransfer
        yield break;
    }

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<RegionalPromptCardViewModel>();
        maskCounter = 0;

        // Clean up old mask files from previous generations
        CleanupOldMaskFiles();

        // Sync canvas size from the generation resolution
        // This ensures masks are rendered at the correct size even if user changed dimensions
        var primarySize = e.Builder.Connections.PrimarySize;
        if (primarySize is { Width: > 0, Height: > 0 })
        {
            card.SetCanvasSize(primarySize.Width, primarySize.Height);
        }

        // Get enabled layers with content
        var enabledLayers = card.GetEnabledLayersWithContent();

        if (enabledLayers.Count == 0)
        {
            // No layers defined, nothing to do
            return;
        }

        // Start with the base positive and negative conditioning
        var currentPositive = e.Temp.Base.Conditioning!.Unwrap().Positive;
        var currentNegative = e.Temp.Base.Conditioning.Negative;

        // Process each layer
        foreach (var layer in enabledLayers)
        {
            // Render layer to mask
            using var maskImage = card.RenderLayerToMask(layer);
            if (maskImage is null)
                continue;

            // Save mask to temp file and add to file transfers
            var maskFileName = GetMaskFileName(layer);
            var tempPath = SaveMaskToTempFile(maskImage, maskFileName);

            // Add to file transfers so it gets uploaded to ComfyUI's input/Inference folder
            e.AddFileTransfer(tempPath, $"input/Inference/{maskFileName}");

            // Load the mask in the workflow
            var loadedMask = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.LoadImageMask
                {
                    Name = e.Nodes.GetUniqueName($"RegionalPrompt_LoadMask_{maskCounter}"),
                    Image = $"Inference/{maskFileName}",
                    Channel = "red",
                }
            );

            // Encode the layer's prompt
            var layerClip = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.CLIPTextEncode
                {
                    Name = e.Nodes.GetUniqueName($"RegionalPrompt_CLIP_{maskCounter}"),
                    Clip = e.Builder.Connections.Base.Clip!,
                    Text = layer.Prompt,
                }
            );

            // Apply the mask to the conditioning
            var maskedConditioning = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ConditioningSetMask
                {
                    Name = e.Nodes.GetUniqueName($"RegionalPrompt_SetMask_{maskCounter}"),
                    Conditioning = layerClip.Output,
                    Mask = loadedMask.Output,
                    Strength = layer.Strength,
                    SetCondArea = layer.ConditioningAreaValue,
                }
            );

            // Combine with the current positive conditioning
            var combined = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ConditioningCombine
                {
                    Name = e.Nodes.GetUniqueName($"RegionalPrompt_Combine_{maskCounter}"),
                    Conditioning1 = currentPositive,
                    Conditioning2 = maskedConditioning.Output,
                }
            );

            currentPositive = combined.Output;

            // Handle per-layer negative prompt if specified
            if (!string.IsNullOrWhiteSpace(layer.NegativePrompt))
            {
                var layerNegClip = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.CLIPTextEncode
                    {
                        Name = e.Nodes.GetUniqueName($"RegionalPrompt_NegCLIP_{maskCounter}"),
                        Clip = e.Builder.Connections.Base.Clip!,
                        Text = layer.NegativePrompt,
                    }
                );

                var maskedNegConditioning = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.ConditioningSetMask
                    {
                        Name = e.Nodes.GetUniqueName($"RegionalPrompt_NegSetMask_{maskCounter}"),
                        Conditioning = layerNegClip.Output,
                        Mask = loadedMask.Output,
                        Strength = layer.Strength,
                        SetCondArea = layer.ConditioningAreaValue,
                    }
                );

                var combinedNeg = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.ConditioningCombine
                    {
                        Name = e.Nodes.GetUniqueName($"RegionalPrompt_NegCombine_{maskCounter}"),
                        Conditioning1 = currentNegative,
                        Conditioning2 = maskedNegConditioning.Output,
                    }
                );

                currentNegative = combinedNeg.Output;
            }

            maskCounter++;
        }

        // Update the base conditioning with our combined regional conditioning
        e.Temp.Base.Conditioning = (currentPositive, currentNegative);

        // Apply to refiner if available
        if (e.Temp.Refiner.Conditioning is not null)
        {
            ApplyToRefiner(e, enabledLayers, card);
        }
    }

    private void ApplyToRefiner(
        ModuleApplyStepEventArgs e,
        IReadOnlyList<MaskLayer> enabledLayers,
        RegionalPromptCardViewModel card
    )
    {
        var refinerPositive = e.Temp.Refiner.Conditioning!.Positive;
        var refinerNegative = e.Temp.Refiner.Conditioning.Negative;
        var refinerMaskCounter = 0;

        foreach (var layer in enabledLayers)
        {
            // Reuse the same mask filename from base model (already uploaded)
            var maskFileName = GetMaskFileName(layer);

            var loadedMask = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.LoadImageMask
                {
                    Name = e.Nodes.GetUniqueName($"Refiner_RegionalPrompt_LoadMask_{refinerMaskCounter}"),
                    Image = $"Inference/{maskFileName}",
                    Channel = "red",
                }
            );

            var layerClip = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.CLIPTextEncode
                {
                    Name = e.Nodes.GetUniqueName($"Refiner_RegionalPrompt_CLIP_{refinerMaskCounter}"),
                    Clip = e.Builder.Connections.Refiner.Clip ?? e.Builder.Connections.Base.Clip!,
                    Text = layer.Prompt,
                }
            );

            var maskedConditioning = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ConditioningSetMask
                {
                    Name = e.Nodes.GetUniqueName($"Refiner_RegionalPrompt_SetMask_{refinerMaskCounter}"),
                    Conditioning = layerClip.Output,
                    Mask = loadedMask.Output,
                    Strength = layer.Strength,
                    SetCondArea = layer.ConditioningAreaValue,
                }
            );

            var combined = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ConditioningCombine
                {
                    Name = e.Nodes.GetUniqueName($"Refiner_RegionalPrompt_Combine_{refinerMaskCounter}"),
                    Conditioning1 = refinerPositive,
                    Conditioning2 = maskedConditioning.Output,
                }
            );

            refinerPositive = combined.Output;
            refinerMaskCounter++;
        }

        e.Temp.Refiner.Conditioning = (refinerPositive, refinerNegative);
    }

    /// <summary>
    /// Cleans up old mask files from previous generations to prevent temp directory accumulation.
    /// </summary>
    private static void CleanupOldMaskFiles()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "StabilityMatrix", "RegionalPrompts");
        if (!Directory.Exists(tempPath))
            return;

        try
        {
            // Delete all regional mask files from previous sessions
            foreach (var file in Directory.GetFiles(tempPath, "regional_mask_*.png"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore individual file deletion errors - file may be in use
                }
            }
        }
        catch
        {
            // Ignore cleanup errors - not critical to generation
        }
    }

    /// <summary>
    /// Generates a unique filename for a layer mask.
    /// </summary>
    private string GetMaskFileName(MaskLayer layer)
    {
        // Use layer name sanitized + counter for uniqueness
        var safeName = layer.Name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
        return $"regional_mask_{safeName}_{maskCounter}.png";
    }

    /// <summary>
    /// Saves a mask image to a temporary file.
    /// </summary>
    private static string SaveMaskToTempFile(SKImage maskImage, string fileName)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "StabilityMatrix", "RegionalPrompts");
        Directory.CreateDirectory(tempPath);

        var filePath = Path.Combine(tempPath, fileName);

        using var data = maskImage.Encode(SKEncodedImageFormat.Png, 100);
        using var fileStream = File.Create(filePath);
        data.SaveTo(fileStream);

        return filePath;
    }
}
