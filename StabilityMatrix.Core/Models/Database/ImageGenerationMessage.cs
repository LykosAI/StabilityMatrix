using LiteDB;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Core.Models.Database;

/// <summary>
/// Represents an individual message in an image generation conversation
/// </summary>
public record ImageGenerationMessage
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// ID of the conversation this message belongs to
    /// </summary>
    public required Guid ConversationId { get; init; }

    /// <summary>
    /// Role of the message sender
    /// </summary>
    public required MessageRole Role { get; init; }

    /// <summary>
    /// Text content of the message
    /// </summary>
    public string? TextContent { get; init; }

    /// <summary>
    /// Path to stored image file (for persistent storage)
    /// </summary>
    public string? ImagePath { get; init; }

    /// <summary>
    /// MIME type of the image
    /// </summary>
    public string? ImageMimeType { get; init; }

    /// <summary>
    /// Thinking/reasoning content from the model (Gemini 3 Pro)
    /// </summary>
    public string? ThinkingContent { get; init; }

    /// <summary>
    /// When the message was sent/received
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
