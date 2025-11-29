using LiteDB;

namespace StabilityMatrix.Core.Models.Database;

/// <summary>
/// Represents a conversation for image generation with a specific provider
/// </summary>
public record ImageGenerationConversation
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Title of the conversation (auto-generated from first prompt)
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Provider ID (e.g., "gemini-2.5-flash", "flux-kontext")
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// When the conversation was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the conversation was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
