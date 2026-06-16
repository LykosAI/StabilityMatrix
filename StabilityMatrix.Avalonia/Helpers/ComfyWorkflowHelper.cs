using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Shared logic for Image Lab ComfyUI workflow builders
/// </summary>
public static class ComfyWorkflowHelper
{
    /// <summary>
    /// Get reference image filenames from the request (input images + conversation history).
    /// Note: Images uploaded via ComfyClient.UploadImageAsync go to the "Inference" subfolder,
    /// using names matching what <see cref="ComfyImageUploadHelper.UploadImagesAsync"/> uploads.
    /// </summary>
    /// <param name="request">Generation request</param>
    /// <param name="maxImages">Maximum number of reference images supported by the provider</param>
    /// <param name="providerPrefix">Prefix for uploaded filenames (e.g. "flux_kontext")</param>
    public static List<string> GetReferenceImageNames(
        ImageGenerationRequest request,
        int maxImages,
        string providerPrefix
    )
    {
        var imageNames = new List<string>();

        // Priority 1: Current input images (uploaded with known names by the provider)
        if (request.InputImages?.Count > 0)
        {
            for (var i = 0; i < Math.Min(request.InputImages.Count, maxImages); i++)
            {
                imageNames.Add($"Inference/{providerPrefix}_input_{i}.png");
            }
        }

        // Priority 2: Most recent image from conversation history (previous generation).
        // Only include if we have room and we actually have the image content
        // (the provider will have uploaded it).
        if (imageNames.Count < maxImages && request.ConversationHistory != null)
        {
            var lastAssistantImage = request.ConversationHistory.LastOrDefault(m =>
                m is { Role: MessageRole.Assistant, ImageContent: not null }
            );

            if (lastAssistantImage?.ImageContent != null)
            {
                imageNames.Add($"Inference/{providerPrefix}_history_latest.png");
            }
        }

        return imageNames;
    }

    /// <summary>
    /// Chains a LoraLoader node for each selected LoRA onto the model and clip connections,
    /// returning the final connections (unchanged if no LoRAs are selected)
    /// </summary>
    public static (ModelNodeConnection Model, ClipNodeConnection Clip) ApplyLoras(
        NodeDictionary nodes,
        IEnumerable<SelectedLora>? loras,
        ModelNodeConnection model,
        ClipNodeConnection clip
    )
    {
        var loraList = loras?.ToList() ?? [];

        for (var i = 0; i < loraList.Count; i++)
        {
            var lora = loraList[i];
            var loraLoader = nodes.AddNamedNode(
                ComfyNodeBuilder.LoraLoader(
                    nodes.GetUniqueName($"LoraLoader_{i + 1}"),
                    model,
                    clip,
                    lora.Model.RelativePath,
                    (double)lora.ModelWeight,
                    (double)lora.ClipWeight
                )
            );
            model = loraLoader.Output1;
            clip = loraLoader.Output2;
        }

        return (model, clip);
    }
}
