using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Shared logic for Image Lab providers that generate via a local ComfyUI backend
/// </summary>
public static class ComfyGenerationHelper
{
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
