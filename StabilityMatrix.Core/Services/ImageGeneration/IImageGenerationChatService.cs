using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Service for managing image generation conversations
/// </summary>
public interface IImageGenerationChatService
{
    /// <summary>
    /// Get all conversations
    /// </summary>
    Task<List<ImageGenerationConversation>> GetConversationsAsync();

    /// <summary>
    /// Get a specific conversation by ID
    /// </summary>
    Task<ImageGenerationConversation?> GetConversationAsync(Guid conversationId);

    /// <summary>
    /// Get all messages for a conversation
    /// </summary>
    Task<List<ImageGenerationMessage>> GetMessagesAsync(Guid conversationId);

    /// <summary>
    /// Create a new conversation
    /// </summary>
    Task<ImageGenerationConversation> CreateConversationAsync(
        string providerId,
        string initialTitle = "New Conversation"
    );

    /// <summary>
    /// Update a conversation
    /// </summary>
    Task UpdateConversationAsync(ImageGenerationConversation conversation);

    /// <summary>
    /// Delete a conversation and all its messages
    /// </summary>
    Task DeleteConversationAsync(Guid conversationId);

    /// <summary>
    /// Send a message and generate a response
    /// </summary>
    Task<(ImageGenerationMessage UserMessage, ImageGenerationMessage? AssistantMessage)> SendMessageAsync(
        Guid conversationId,
        string? textPrompt,
        List<string>? imagePaths = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Send a message and generate a response with provider options
    /// </summary>
    Task<(ImageGenerationMessage UserMessage, ImageGenerationMessage? AssistantMessage)> SendMessageAsync(
        Guid conversationId,
        string? textPrompt,
        List<string>? imagePaths,
        Dictionary<string, object>? providerOptions,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get available providers
    /// </summary>
    List<IImageGenerationProvider> GetAvailableProviders();

    /// <summary>
    /// Get a provider by ID
    /// </summary>
    IImageGenerationProvider? GetProvider(string providerId);
}
