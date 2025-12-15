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
    /// Delete a specific message from a conversation
    /// </summary>
    /// <param name="messageId">The message ID to delete</param>
    /// <param name="preserveImageFile">If true, keeps the image file on disk (for regenerate). If false, deletes it (for edit/delete)</param>
    Task DeleteMessageAsync(Guid messageId, bool preserveImageFile = false);

    /// <summary>
    /// Remove a specific image from a message. If it's the last image, deletes the entire message.
    /// </summary>
    /// <param name="messageId">The message ID</param>
    /// <param name="imagePath">The specific image path to remove</param>
    /// <returns>True if the entire message was deleted, false if only the image was removed</returns>
    Task<bool> RemoveImageFromMessageAsync(Guid messageId, string imagePath);

    /// <summary>
    /// Get a specific message by ID
    /// </summary>
    Task<ImageGenerationMessage?> GetMessageAsync(Guid messageId);

    /// <summary>
    /// Update a message's text content without affecting other messages or triggering regeneration
    /// </summary>
    /// <param name="messageId">The message ID to update</param>
    /// <param name="newTextContent">The new text content</param>
    /// <returns>The updated message, or null if not found</returns>
    Task<ImageGenerationMessage?> UpdateMessageTextAsync(Guid messageId, string newTextContent);

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
        IProgress<ImageGenerationProgress>? progress = null,
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
        IProgress<ImageGenerationProgress>? progress = null,
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
