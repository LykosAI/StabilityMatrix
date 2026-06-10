using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Shared logic for Image Lab providers that generate via a local ComfyUI backend
/// </summary>
public static class ComfyGenerationHelper
{
    /// <summary>
    /// Builds the failure response for a workflow ComfyUI rejected at queue time
    /// (Refit <see cref="ApiException"/> with the validation JSON in the body). The raw
    /// body is carried in <see cref="ImageGenerationResponse.ErrorDetailJson"/> so the UI
    /// can show it in a detail dialog.
    /// </summary>
    public static ImageGenerationResponse CreateWorkflowRejectedResponse(
        ApiException apiEx,
        string providerName,
        ILogger logger
    )
    {
        var detail = !string.IsNullOrWhiteSpace(apiEx.Content) ? apiEx.Content : apiEx.Message;
        logger.LogError(
            apiEx,
            "ComfyUI rejected {Provider} workflow ({StatusCode}): {Detail}",
            providerName,
            apiEx.StatusCode,
            detail
        );

        return new ImageGenerationResponse
        {
            IsSuccess = false,
            ErrorMessage =
                $"ComfyUI rejected the workflow ({(int)apiEx.StatusCode}): {Truncate(detail, 800)}",
            ErrorDetailJson = string.IsNullOrWhiteSpace(apiEx.Content) ? null : apiEx.Content,
        };
    }

    /// <summary>
    /// Builds the failure response for a node that failed during execution
    /// (<see cref="ComfyNodeException"/>, e.g. a tensor-shape mismatch from a wrong
    /// encoder pairing). The full error JSON is carried in
    /// <see cref="ImageGenerationResponse.ErrorDetailJson"/> for the detail dialog.
    /// </summary>
    public static ImageGenerationResponse CreateNodeErrorResponse(
        ComfyNodeException nodeEx,
        string providerName,
        ILogger logger
    )
    {
        logger.LogError(
            nodeEx,
            "ComfyUI node execution failed for {Provider}: {Json}",
            providerName,
            nodeEx.JsonData
        );

        var nodeType = nodeEx.ErrorData.NodeType;
        var exceptionMessage = nodeEx.ErrorData.ExceptionMessage ?? "Unknown error";

        return new ImageGenerationResponse
        {
            IsSuccess = false,
            ErrorMessage = string.IsNullOrEmpty(nodeType)
                ? $"ComfyUI node execution failed: {Truncate(exceptionMessage, 400)}"
                : $"ComfyUI node '{nodeType}' failed: {Truncate(exceptionMessage, 400)}",
            ErrorDetailJson = nodeEx.JsonData,
        };
    }

    /// <summary>
    /// Selects the output images from an executed prompt's outputs: prefers the
    /// "SaveImage" node deterministically, otherwise the first non-empty output
    /// by ordinal key order. Returns (null, null) if no output has images.
    /// </summary>
    public static (string? OutputKey, List<ComfyImage>? Images) SelectOutputImages(
        Dictionary<string, List<ComfyImage>?> outputImages
    )
    {
        const string preferredOutputKey = "SaveImage";

        if (
            outputImages.TryGetValue(preferredOutputKey, out var preferredImages)
            && preferredImages is { Count: > 0 }
        )
        {
            return (preferredOutputKey, preferredImages);
        }

        var selected = outputImages
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .FirstOrDefault(kvp => kvp.Value is { Count: > 0 });

        return (string.IsNullOrEmpty(selected.Key) ? null : selected.Key, selected.Value);
    }

    /// <summary>
    /// Gets the MIME type for a generated image filename by extension, defaulting to PNG
    /// </summary>
    public static string GetMimeTypeForFileName(string fileName) =>
        fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
        : fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
        : fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
        : "image/png";

    /// <summary>
    /// Truncates a string to at most <paramref name="maxLength"/> characters, appending "..."
    /// </summary>
    public static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
