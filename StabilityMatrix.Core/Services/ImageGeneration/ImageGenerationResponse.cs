namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Response from image generation
/// </summary>
public record ImageGenerationResponse
{
    /// <summary>
    /// Generated images (base64 encoded)
    /// </summary>
    public List<GeneratedImage>? Images { get; init; }

    /// <summary>
    /// Text response from the model (if any)
    /// </summary>
    public string? TextResponse { get; init; }

    /// <summary>
    /// Thinking/reasoning content from the model (Gemini 3 Pro)
    /// </summary>
    public string? ThinkingContent { get; init; }

    /// <summary>
    /// Thought signature from Gemini API response.
    /// Must be stored and passed back in follow-up requests.
    /// See: https://ai.google.dev/gemini-api/docs/thought-signatures
    /// </summary>
    public string? ThoughtSignature { get; init; }

    /// <summary>
    /// Whether the generation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if generation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Provider-specific metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents a generated image
/// </summary>
public record GeneratedImage
{
    /// <summary>
    /// Base64 encoded image data
    /// </summary>
    public required string Base64Data { get; init; }

    /// <summary>
    /// MIME type (e.g., "image/png", "image/jpeg")
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Thought signature for this specific image from Gemini API.
    /// Must be passed back in follow-up requests.
    /// </summary>
    public string? ThoughtSignature { get; init; }
}
