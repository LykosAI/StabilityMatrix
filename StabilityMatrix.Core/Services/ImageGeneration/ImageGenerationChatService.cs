using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Service for managing image generation conversations
/// </summary>
public class ImageGenerationChatService(
    ILogger<ImageGenerationChatService> logger,
    IBananaVisionDbContext database,
    ISettingsManager settingsManager,
    IEnumerable<IImageGenerationProvider> providers
) : IImageGenerationChatService
{
    private readonly List<IImageGenerationProvider> providers = providers.ToList();

    public async Task<List<ImageGenerationConversation>> GetConversationsAsync()
    {
        logger.LogDebug("Querying conversations from database...");
        var conversations = await database
            .Conversations.Query()
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync()
            .ConfigureAwait(false);

        logger.LogInformation("Retrieved {Count} conversations from database", conversations.Count);
        return conversations;
    }

    public async Task<ImageGenerationConversation?> GetConversationAsync(Guid conversationId)
    {
        return await database.Conversations.FindByIdAsync(conversationId).ConfigureAwait(false);
    }

    public async Task<List<ImageGenerationMessage>> GetMessagesAsync(Guid conversationId)
    {
        var messages = await database
            .Messages.Query()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync()
            .ConfigureAwait(false);

        return messages;
    }

    public async Task<ImageGenerationConversation> CreateConversationAsync(
        string providerId,
        string initialTitle = "New Conversation"
    )
    {
        var conversation = new ImageGenerationConversation { Title = initialTitle, ProviderId = providerId };

        await database.Conversations.InsertAsync(conversation).ConfigureAwait(false);

        logger.LogInformation(
            "Created new conversation {ConversationId} with provider {ProviderId}",
            conversation.Id,
            providerId
        );

        return conversation;
    }

    public async Task UpdateConversationAsync(ImageGenerationConversation conversation)
    {
        var updated = conversation with { UpdatedAt = DateTime.UtcNow };
        await database.Conversations.UpdateAsync(updated).ConfigureAwait(false);
    }

    public async Task DeleteConversationAsync(Guid conversationId)
    {
        // Delete all messages first
        var messages = await GetMessagesAsync(conversationId).ConfigureAwait(false);
        foreach (var message in messages)
        {
            // Delete associated image files
            if (!string.IsNullOrEmpty(message.ImagePath) && File.Exists(message.ImagePath))
            {
                try
                {
                    File.Delete(message.ImagePath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete image file {ImagePath}", message.ImagePath);
                }
            }

            await database.Messages.DeleteAsync(message.Id).ConfigureAwait(false);
        }

        // Delete the conversation
        await database.Conversations.DeleteAsync(conversationId).ConfigureAwait(false);

        logger.LogInformation(
            "Deleted conversation {ConversationId} and {MessageCount} messages",
            conversationId,
            messages.Count
        );
    }

    public Task<(
        ImageGenerationMessage UserMessage,
        ImageGenerationMessage? AssistantMessage
    )> SendMessageAsync(
        Guid conversationId,
        string? textPrompt,
        List<string>? imagePaths = null,
        CancellationToken cancellationToken = default
    )
    {
        return SendMessageAsync(conversationId, textPrompt, imagePaths, null, cancellationToken);
    }

    public async Task<(
        ImageGenerationMessage UserMessage,
        ImageGenerationMessage? AssistantMessage
    )> SendMessageAsync(
        Guid conversationId,
        string? textPrompt,
        List<string>? imagePaths,
        Dictionary<string, object>? providerOptions,
        CancellationToken cancellationToken = default
    )
    {
        var conversation = await GetConversationAsync(conversationId).ConfigureAwait(false);
        if (conversation == null)
        {
            throw new InvalidOperationException($"Conversation {conversationId} not found");
        }

        var provider = GetProvider(conversation.ProviderId);
        if (provider == null)
        {
            throw new InvalidOperationException($"Provider {conversation.ProviderId} not found");
        }

        // Create user message
        var userMessage = new ImageGenerationMessage
        {
            ConversationId = conversationId,
            Role = MessageRole.User,
            TextContent = textPrompt,
        };

        // Handle image inputs if provided
        List<ImageInputData>? inputImages = null;
        if (imagePaths?.Count > 0)
        {
            inputImages = [];
            foreach (var imagePath in imagePaths.Where(File.Exists))
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken)
                    .ConfigureAwait(false);
                var base64 = Convert.ToBase64String(imageBytes);
                var mimeType = GetMimeTypeFromPath(imagePath);

                inputImages.Add(new ImageInputData { Base64Data = base64, MimeType = mimeType });

                // Save reference to first input image in user message
                if (inputImages.Count == 1)
                {
                    userMessage = userMessage with { ImagePath = imagePath, ImageMimeType = mimeType };
                }
            }
        }

        await database.Messages.InsertAsync(userMessage).ConfigureAwait(false);

        // Get conversation history (load image files and convert to base64 for providers)
        var previousMessages = await GetMessagesAsync(conversationId).ConfigureAwait(false);
        logger.LogInformation(
            "Building conversation history with {Count} previous messages",
            previousMessages.Count - 1
        );

        var conversationHistory = new List<ConversationMessage>();

        foreach (var m in previousMessages.Where(msg => msg.Id != userMessage.Id))
        {
            ImageInputData? imageContent = null;

            // Load image from file if it exists
            if (!string.IsNullOrEmpty(m.ImagePath) && File.Exists(m.ImagePath))
            {
                try
                {
                    var imageBytes = await File.ReadAllBytesAsync(m.ImagePath, cancellationToken)
                        .ConfigureAwait(false);
                    imageContent = new ImageInputData
                    {
                        Base64Data = Convert.ToBase64String(imageBytes),
                        MimeType = m.ImageMimeType ?? "image/png",
                        FilePath = m.ImagePath, // Include file path for local providers
                    };

                    logger.LogInformation(
                        "Loaded history image for {Role} message: {ImagePath} ({Size} bytes)",
                        m.Role,
                        m.ImagePath,
                        imageBytes.Length
                    );
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to load conversation history image from {ImagePath}",
                        m.ImagePath
                    );
                }
            }

            conversationHistory.Add(
                new ConversationMessage
                {
                    Role = m.Role,
                    TextContent = m.TextContent,
                    ImageContent = imageContent,
                }
            );
        }

        // Build request
        var request = new ImageGenerationRequest
        {
            TextPrompt = textPrompt,
            InputImages = inputImages,
            ConversationHistory = conversationHistory,
            ProviderOptions = providerOptions,
        };

        // Generate response
        logger.LogInformation(
            "Generating image with provider {ProviderId} for conversation {ConversationId}",
            provider.ProviderId,
            conversationId
        );

        var response = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            logger.LogError("Image generation failed: {ErrorMessage}", response.ErrorMessage);

            // Create error message
            var errorMessage = new ImageGenerationMessage
            {
                ConversationId = conversationId,
                Role = MessageRole.Assistant,
                TextContent = $"Error: {response.ErrorMessage}",
            };

            await database.Messages.InsertAsync(errorMessage).ConfigureAwait(false);

            // Update conversation
            var updatedConversation = conversation with
            {
                UpdatedAt = DateTime.UtcNow,
            };
            await database.Conversations.UpdateAsync(updatedConversation).ConfigureAwait(false);

            return (userMessage, errorMessage);
        }

        // Save generated images
        string? savedImagePath = null;
        if (response.Images?.Count > 0)
        {
            var outputDir = GetOutputDirectory();
            Directory.CreateDirectory(outputDir);

            var firstImage = response.Images[0];
            var imageBytes = Convert.FromBase64String(firstImage.Base64Data);
            var extension = GetExtensionFromMimeType(firstImage.MimeType);
            var fileName = $"banana_{DateTime.UtcNow:yyyyMMdd_HHmmss}{extension}";
            savedImagePath = Path.Combine(outputDir, fileName);

            await File.WriteAllBytesAsync(savedImagePath, imageBytes, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Saved generated image to {ImagePath}", savedImagePath);
        }

        // Create assistant message
        var assistantMessage = new ImageGenerationMessage
        {
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            TextContent = response.TextResponse,
            ImagePath = savedImagePath,
            ImageMimeType = response.Images?.FirstOrDefault()?.MimeType,
            ThinkingContent = response.ThinkingContent,
        };

        await database.Messages.InsertAsync(assistantMessage).ConfigureAwait(false);

        // Update conversation title if this is the first exchange
        if (previousMessages.Count == 0 && !string.IsNullOrEmpty(textPrompt))
        {
            var title = textPrompt.Length > 50 ? textPrompt[..50] + "..." : textPrompt;
            conversation = conversation with { Title = title, UpdatedAt = DateTime.UtcNow };
        }
        else
        {
            conversation = conversation with { UpdatedAt = DateTime.UtcNow };
        }

        await database.Conversations.UpdateAsync(conversation).ConfigureAwait(false);

        return (userMessage, assistantMessage);
    }

    public List<IImageGenerationProvider> GetAvailableProviders()
    {
        return providers;
    }

    public IImageGenerationProvider? GetProvider(string providerId)
    {
        return providers.FirstOrDefault(p => p.ProviderId == providerId);
    }

    private string GetOutputDirectory()
    {
        var outputDir = Path.Combine(settingsManager.ImagesDirectory, "BananaVision");
        return outputDir;
    }

    private static string GetMimeTypeFromPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png",
        };
    }

    private static string GetExtensionFromMimeType(string mimeType)
    {
        return mimeType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".png",
        };
    }
}
