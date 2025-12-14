namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Request for image generation
/// </summary>
public record ImageGenerationRequest
{
    /// <summary>
    /// Text prompt for generation
    /// </summary>
    public string? TextPrompt { get; init; }

    /// <summary>
    /// Input images for editing or composition (base64 encoded)
    /// </summary>
    public List<ImageInputData>? InputImages { get; init; }

    /// <summary>
    /// Previous conversation history for multi-turn support
    /// </summary>
    public List<ConversationMessage>? ConversationHistory { get; init; }

    /// <summary>
    /// Provider-specific configuration options
    /// </summary>
    public Dictionary<string, object>? ProviderOptions { get; init; }

    /// <summary>
    /// Optional progress reporter for providers that can emit generation progress (e.g., local ComfyUI).
    /// </summary>
    public IProgress<ImageGenerationProgress>? Progress { get; init; }
}

/// <summary>
/// Represents an input image with its data
/// </summary>
public record ImageInputData
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
    /// Optional file path on disk (for local providers that can upload directly)
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Thought signature from Gemini API for this image.
    /// Must be passed back in follow-up requests to preserve reasoning context.
    /// </summary>
    public string? ThoughtSignature { get; init; }
}

/// <summary>
/// Represents a message in the conversation history
/// </summary>
public record ConversationMessage
{
    /// <summary>
    /// Role of the message sender
    /// </summary>
    public required MessageRole Role { get; init; }

    /// <summary>
    /// Text content of the message
    /// </summary>
    public string? TextContent { get; init; }

    /// <summary>
    /// Image content (base64 encoded)
    /// </summary>
    public ImageInputData? ImageContent { get; init; }

    /// <summary>
    /// Thought signature for text parts from Gemini API.
    /// Must be passed back in follow-up requests to preserve reasoning context.
    /// </summary>
    public string? TextThoughtSignature { get; init; }
}

/// <summary>
/// Role of a message sender
/// </summary>
public enum MessageRole
{
    User,
    Assistant,
    System,
}
