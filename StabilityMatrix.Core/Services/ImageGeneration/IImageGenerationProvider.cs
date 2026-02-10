namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Represents a provider for image generation (e.g., Gemini, Flux Kontext)
/// </summary>
public interface IImageGenerationProvider
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "gemini-2.5-flash", "flux-kontext")
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Display name for this provider
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this provider supports image input for editing/composition
    /// </summary>
    bool SupportsImageInput { get; }

    /// <summary>
    /// Whether this provider supports multi-turn conversations
    /// </summary>
    bool SupportsMultiTurn { get; }

    /// <summary>
    /// Whether this provider requires thought signatures on image parts (Gemini 3 Pro).
    /// If true, conversations started with non-thinking providers cannot be continued.
    /// </summary>
    bool RequiresThoughtSignatures { get; }

    /// <summary>
    /// Generate an image based on the provided request
    /// </summary>
    Task<ImageGenerationResponse> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default
    );
}
