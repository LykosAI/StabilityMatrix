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
    /// Send a message and generate a response using the specified provider
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="providerId">The provider to use for this message</param>
    /// <param name="textPrompt">Optional text prompt</param>
    /// <param name="imagePaths">Optional image paths to include</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<(ImageGenerationMessage UserMessage, ImageGenerationMessage? AssistantMessage)> SendMessageAsync(
        Guid conversationId,
        string providerId,
        string? textPrompt,
        List<string>? imagePaths = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Send a message and generate a response with provider options using the specified provider
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="providerId">The provider to use for this message</param>
    /// <param name="textPrompt">Optional text prompt</param>
    /// <param name="imagePaths">Optional image paths to include</param>
    /// <param name="providerOptions">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<(ImageGenerationMessage UserMessage, ImageGenerationMessage? AssistantMessage)> SendMessageAsync(
        Guid conversationId,
        string providerId,
        string? textPrompt,
        List<string>? imagePaths,
        Dictionary<string, object>? providerOptions,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retry generation for the last user message in a conversation.
    /// Does not create a new user message - just regenerates the assistant response.
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="providerId">The provider to use for regeneration</param>
    /// <param name="providerOptions">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated assistant message</returns>
    Task<ImageGenerationMessage> RetryGenerationAsync(
        Guid conversationId,
        string providerId,
        Dictionary<string, object>? providerOptions = null,
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
