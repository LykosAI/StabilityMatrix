namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Contains constant provider IDs for BananaVision image generation providers.
/// Use these constants instead of hardcoded strings to avoid typos and enable refactoring.
/// </summary>
public static class BananaVisionProviderIds
{
    /// <summary>
    /// Gemini 2.5 Flash image generation provider (Nano Banana)
    /// </summary>
    public const string Gemini25Flash = "gemini-2.5-flash";

    /// <summary>
    /// Gemini 3.1 Flash image generation provider (Nano Banana 2) with thinking support
    /// </summary>
    public const string Gemini31Flash = "gemini-3.1-flash";

    /// <summary>
    /// Gemini 3 Pro image generation provider with thinking support
    /// </summary>
    public const string Gemini3Pro = "gemini-3-pro";

    /// <summary>
    /// Flux Kontext local image generation provider (requires ComfyUI)
    /// </summary>
    public const string FluxKontext = "flux-kontext";

    /// <summary>
    /// Qwen Image Edit local image generation provider (requires ComfyUI)
    /// </summary>
    public const string QwenImageEdit = "qwen-image-edit";

    /// <summary>
    /// Flux.2 Klein local image generation provider (requires ComfyUI)
    /// </summary>
    public const string Flux2Klein = "flux2-klein";

    /// <summary>
    /// Check if a provider ID is a local provider that requires ComfyUI backend
    /// </summary>
    public static bool IsLocalProvider(string? providerId) =>
        providerId is FluxKontext or QwenImageEdit or Flux2Klein;

    /// <summary>
    /// Check if a provider ID is a cloud/API provider (Gemini)
    /// </summary>
    public static bool IsCloudProvider(string? providerId) => providerId?.Contains("gemini") == true;

    /// <summary>
    /// Check if a provider ID supports thinking/reasoning output
    /// </summary>
    public static bool SupportsThinking(string? providerId) => providerId is Gemini3Pro or Gemini31Flash;
}
