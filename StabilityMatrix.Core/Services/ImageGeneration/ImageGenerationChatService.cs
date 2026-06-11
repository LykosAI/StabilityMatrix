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

    private static List<string> GetMessageImagePaths(ImageGenerationMessage message)
    {
        var paths = new List<string>();

        if (message.ImagePaths is { Count: > 0 })
        {
            paths.AddRange(message.ImagePaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
        else if (!string.IsNullOrEmpty(message.ImagePath))
        {
            paths.Add(message.ImagePath);
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsPathUnderDirectory(string filePath, string directoryPath)
    {
        var fullFilePath = Path.GetFullPath(filePath);
        var fullDirectoryPath = Path.GetFullPath(directoryPath);

        var relativePath = Path.GetRelativePath(fullDirectoryPath, fullFilePath);
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        if (Path.IsPathRooted(relativePath))
            return false;

        return !relativePath.StartsWith("..", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string CreateOutputFileName(string extension)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var shortGuid = Guid.NewGuid().ToString("N")[..8];
        return $"imagelab_{timestamp}_{shortGuid}{extension}";
    }

    private static string GenerateConversationTitle(string textPrompt)
    {
        var title = textPrompt.Trim();
        if (title.Length == 0)
            return "New Conversation";

        var firstSentenceEnd = title.IndexOfAny(['.', '!', '?']);
        if (firstSentenceEnd > 0 && firstSentenceEnd < 50)
        {
            title = title[..firstSentenceEnd].Trim();
        }
        else if (title.Length > 50)
        {
            title = title[..50].TrimEnd() + "...";
        }

        return title.Length == 0 ? "New Conversation" : title;
    }

    private string GetOutputDirectory()
    {
        return Path.Combine(settingsManager.ImagesDirectory, "ImageLab");
    }

    private string GetInputDirectory(Guid conversationId)
    {
        return Path.Combine(GetOutputDirectory(), "Inputs", conversationId.ToString("N"));
    }

    private async Task<string?> PersistInputImageAsync(
        Guid conversationId,
        string sourcePath,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!File.Exists(sourcePath))
                return null;

            var inputDir = GetInputDirectory(conversationId);
            Directory.CreateDirectory(inputDir);

            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var shortGuid = Guid.NewGuid().ToString("N")[..8];
            var fileName = $"input_{timestamp}_{shortGuid}{extension}";
            var destinationPath = Path.Combine(inputDir, fileName);

            await using var sourceStream = File.OpenRead(sourcePath);
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);

            return destinationPath;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist input image {SourcePath}", sourcePath);
            return null;
        }
    }

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
        var outputDir = GetOutputDirectory();
        var inputDir = GetInputDirectory(conversationId);
        var deletedImageCount = 0;

        foreach (var message in messages)
        {
            var imagePaths = new List<string>();
            if (!string.IsNullOrEmpty(message.ImagePath))
            {
                imagePaths.Add(message.ImagePath);
            }
            if (message.ImagePaths is { Count: > 0 })
            {
                imagePaths.AddRange(message.ImagePaths.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            foreach (var imagePathValue in imagePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(imagePathValue))
                    continue;

                // Delete generated output images (output dir) and app-managed input copies (input dir).
                // Never delete arbitrary user filesystem paths.
                if (
                    IsPathUnderDirectory(imagePathValue, outputDir)
                    || IsPathUnderDirectory(imagePathValue, inputDir)
                )
                {
                    try
                    {
                        File.Delete(imagePathValue);
                        deletedImageCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to delete generated image file {ImagePath}",
                            imagePathValue
                        );
                    }
                }
                else
                {
                    logger.LogDebug(
                        "Preserving user input image {ImagePath} (not in output directory)",
                        imagePathValue
                    );
                }
            }

            await database.Messages.DeleteAsync(message.Id).ConfigureAwait(false);
        }

        // Delete the conversation
        await database.Conversations.DeleteAsync(conversationId).ConfigureAwait(false);

        logger.LogInformation(
            "Deleted conversation {ConversationId} with {MessageCount} messages and {ImageCount} generated images",
            conversationId,
            messages.Count,
            deletedImageCount
        );
    }

    public async Task DeleteMessageAsync(Guid messageId, bool preserveImageFile = false)
    {
        var message = await database.Messages.FindByIdAsync(messageId).ConfigureAwait(false);
        if (message == null)
        {
            logger.LogWarning("Message {MessageId} not found", messageId);
            return;
        }

        var imagePaths = new List<string>();
        if (!string.IsNullOrEmpty(message.ImagePath))
        {
            imagePaths.Add(message.ImagePath);
        }
        if (message.ImagePaths is { Count: > 0 })
        {
            imagePaths.AddRange(message.ImagePaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        // Delete generated image files if they exist (unless we're preserving output images).
        // Also delete app-managed input copies when present (safe to delete).
        if (!preserveImageFile)
        {
            var outputDir = GetOutputDirectory();
            var inputDir = GetInputDirectory(message.ConversationId);

            foreach (var imagePathValue in imagePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(imagePathValue))
                    continue;

                if (
                    !IsPathUnderDirectory(imagePathValue, outputDir)
                    && !IsPathUnderDirectory(imagePathValue, inputDir)
                )
                    continue;

                try
                {
                    File.Delete(imagePathValue);
                    logger.LogDebug("Deleted managed image file {ImagePath}", imagePathValue);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to delete generated image file {ImagePath}",
                        imagePathValue
                    );
                }
            }
        }
        else if (preserveImageFile && imagePaths.Count > 0)
        {
            logger.LogDebug("Preserved image file(s) for message {MessageId} (regenerate mode)", message.Id);
        }

        await database.Messages.DeleteAsync(messageId).ConfigureAwait(false);
        logger.LogDebug("Deleted message {MessageId}", messageId);
    }

    public async Task<ImageGenerationMessage?> GetMessageAsync(Guid messageId)
    {
        return await database.Messages.FindByIdAsync(messageId).ConfigureAwait(false);
    }

    public async Task<ImageGenerationMessage?> UpdateMessageTextAsync(Guid messageId, string newTextContent)
    {
        var message = await database.Messages.FindByIdAsync(messageId).ConfigureAwait(false);
        if (message == null)
        {
            logger.LogWarning("Message {MessageId} not found for text update", messageId);
            return null;
        }

        var updatedMessage = message with { TextContent = newTextContent };
        await database.Messages.UpdateAsync(updatedMessage).ConfigureAwait(false);

        logger.LogDebug("Updated text content for message {MessageId}", messageId);
        return updatedMessage;
    }

    public async Task<bool> RemoveImageFromMessageAsync(Guid messageId, string imagePath)
    {
        var message = await database.Messages.FindByIdAsync(messageId).ConfigureAwait(false);
        if (message == null)
        {
            logger.LogWarning("Message {MessageId} not found for image removal", messageId);
            return true; // Treat as fully deleted
        }

        var allImagePaths = GetMessageImagePaths(message).ToList();

        // If this is the only image (or no images), delete the whole message
        if (allImagePaths.Count <= 1)
        {
            await DeleteMessageAsync(messageId).ConfigureAwait(false);
            return true;
        }

        // Remove the specific image path
        var updatedPaths = allImagePaths
            .Where(p => !string.Equals(p, imagePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Delete the image file
        if (File.Exists(imagePath))
        {
            try
            {
                File.Delete(imagePath);
                logger.LogDebug("Deleted image file {ImagePath}", imagePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete image file {ImagePath}", imagePath);
            }
        }

        // Update the message with remaining paths
        var updatedMessage = message with
        {
            ImagePath = updatedPaths.FirstOrDefault(),
            ImagePaths = updatedPaths.Count > 0 ? updatedPaths : null,
        };

        await database.Messages.UpdateAsync(updatedMessage).ConfigureAwait(false);
        logger.LogDebug(
            "Removed image {ImagePath} from message {MessageId}, {RemainingCount} images remaining",
            imagePath,
            messageId,
            updatedPaths.Count
        );

        return false;
    }

    public Task<(
        ImageGenerationMessage UserMessage,
        ImageGenerationMessage? AssistantMessage
    )> SendMessageAsync(
        Guid conversationId,
        string providerId,
        string? textPrompt,
        List<string>? imagePaths = null,
        CancellationToken cancellationToken = default
    )
    {
        return SendMessageAsync(
            conversationId,
            providerId,
            textPrompt,
            imagePaths,
            null,
            null,
            cancellationToken
        );
    }

    public async Task<(
        ImageGenerationMessage UserMessage,
        ImageGenerationMessage? AssistantMessage
    )> SendMessageAsync(
        Guid conversationId,
        string providerId,
        string? textPrompt,
        List<string>? imagePaths,
        Dictionary<string, object>? providerOptions,
        IProgress<ImageGenerationProgress>? progress,
        CancellationToken cancellationToken = default
    )
    {
        var conversation = await GetConversationAsync(conversationId).ConfigureAwait(false);
        if (conversation == null)
        {
            throw new InvalidOperationException($"Conversation {conversationId} not found");
        }

        var provider = GetProvider(providerId);
        if (provider == null)
        {
            throw new InvalidOperationException($"Provider {providerId} not found");
        }

        // Update conversation's provider if it changed
        var providerChanged = conversation.ProviderId != providerId;
        if (providerChanged)
        {
            logger.LogInformation(
                "Switching conversation {ConversationId} provider from {OldProvider} to {NewProvider}",
                conversationId,
                conversation.ProviderId,
                providerId
            );
            conversation.ProviderId = providerId;
        }

        // Check for provider compatibility - thought signature requirements
        // If switching to a thinking model with incompatible history, we'll carry forward
        // the last output image as an input instead of using the full history
        string? carryForwardImagePath = null;
        if (provider.RequiresThoughtSignatures)
        {
            var existingMessages = await GetMessagesAsync(conversationId).ConfigureAwait(false);
            var incompatibleMessages = existingMessages
                .Where(m =>
                    m.Role == MessageRole.Assistant
                    && !string.IsNullOrEmpty(m.ImagePath)
                    && string.IsNullOrEmpty(m.ThoughtSignature)
                )
                .ToList();

            if (incompatibleMessages.Count > 0)
            {
                // Find the last assistant message with an image to carry forward
                var lastAssistantImage = existingMessages
                    .Where(m => m.Role == MessageRole.Assistant && !string.IsNullOrEmpty(m.ImagePath))
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                if (lastAssistantImage != null)
                {
                    carryForwardImagePath = GetMessageImagePaths(lastAssistantImage)
                        .FirstOrDefault(File.Exists);
                    if (carryForwardImagePath != null)
                    {
                        logger.LogInformation(
                            "Switching to thinking model with incompatible history. "
                                + "Carrying forward last image as input: {ImagePath}",
                            carryForwardImagePath
                        );
                    }
                }
            }
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
        var persistedImagePaths = new List<string>();

        // If we have a carry-forward image (from incompatible history), add it first
        if (!string.IsNullOrEmpty(carryForwardImagePath) && File.Exists(carryForwardImagePath))
        {
            inputImages = [];
            var imageBytes = await File.ReadAllBytesAsync(carryForwardImagePath, cancellationToken)
                .ConfigureAwait(false);
            var base64 = Convert.ToBase64String(imageBytes);
            var mimeType = GetMimeTypeFromPath(carryForwardImagePath);

            inputImages.Add(
                new ImageInputData
                {
                    Base64Data = base64,
                    MimeType = mimeType,
                    FilePath = carryForwardImagePath,
                }
            );
            // Note: We don't persist the carry-forward image as it's already persisted
            // and we don't want to duplicate it in the user message metadata
        }

        if (imagePaths?.Count > 0)
        {
            inputImages ??= [];
            foreach (var originalImagePath in imagePaths.Where(File.Exists))
            {
                var persistedPath = await PersistInputImageAsync(
                        conversationId,
                        originalImagePath,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                var imagePathToUse = persistedPath ?? originalImagePath;

                var imageBytes = await File.ReadAllBytesAsync(imagePathToUse, cancellationToken)
                    .ConfigureAwait(false);
                var base64 = Convert.ToBase64String(imageBytes);
                var mimeType = GetMimeTypeFromPath(imagePathToUse);

                inputImages.Add(
                    new ImageInputData
                    {
                        Base64Data = base64,
                        MimeType = mimeType,
                        FilePath = imagePathToUse,
                    }
                );
                persistedImagePaths.Add(imagePathToUse);

                // Stop if cancellation requested
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (persistedImagePaths.Count > 0)
            {
                userMessage = userMessage with
                {
                    ImagePath = persistedImagePaths[0],
                    ImageMimeType = inputImages.First(i => i.FilePath == persistedImagePaths[0]).MimeType,
                    ImagePaths = persistedImagePaths,
                };
            }
        }

        await database.Messages.InsertAsync(userMessage).ConfigureAwait(false);

        // Update conversation title if it's still the default
        if (conversation.Title == "New Conversation" && !string.IsNullOrEmpty(textPrompt))
        {
            var newTitle = GenerateConversationTitle(textPrompt);
            conversation = conversation with
            {
                Title = newTitle,
                ProviderId = providerId, // Update provider ID as well
                UpdatedAt = DateTime.UtcNow,
            };

            await database.Conversations.UpdateAsync(conversation).ConfigureAwait(false);
            logger.LogDebug("Updated conversation title to: {Title}", newTitle);
        }

        // Get conversation history (load image files and convert to base64 for providers)
        // If we're carrying forward an image (due to incompatible history), skip the history
        var conversationHistory = new List<ConversationMessage>();

        if (string.IsNullOrEmpty(carryForwardImagePath))
        {
            var previousMessages = await GetMessagesAsync(conversationId).ConfigureAwait(false);
            logger.LogInformation(
                "Building conversation history with {Count} previous messages",
                previousMessages.Count - 1
            );

            foreach (var m in previousMessages.Where(msg => msg.Id != userMessage.Id))
            {
                var messageImagePaths = GetMessageImagePaths(m).Where(File.Exists).ToList();

                // If no images, keep a single message with text (and thought signature for text parts).
                if (messageImagePaths.Count == 0)
                {
                    conversationHistory.Add(
                        new ConversationMessage
                        {
                            Role = m.Role,
                            TextContent = m.TextContent,
                            ImageContent = null,
                            TextThoughtSignature = m.ThoughtSignature,
                        }
                    );
                    continue;
                }

                // If there are multiple images, emit one history entry per image.
                // Include the message text only on the first entry to avoid duplicating prompt text.
                for (var i = 0; i < messageImagePaths.Count; i++)
                {
                    var imagePath = messageImagePaths[i];
                    ImageInputData? imageContent = null;

                    try
                    {
                        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken)
                            .ConfigureAwait(false);
                        imageContent = new ImageInputData
                        {
                            Base64Data = Convert.ToBase64String(imageBytes),
                            MimeType = GetMimeTypeFromPath(imagePath),
                            FilePath = imagePath,
                            ThoughtSignature = m.ThoughtSignature,
                        };

                        logger.LogInformation(
                            "Loaded history image for {Role} message: {ImagePath} ({Size} bytes){ThoughtSig}",
                            m.Role,
                            imagePath,
                            imageBytes.Length,
                            !string.IsNullOrEmpty(m.ThoughtSignature) ? " [with thought signature]" : ""
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to load conversation history image from {ImagePath}",
                            imagePath
                        );
                    }

                    conversationHistory.Add(
                        new ConversationMessage
                        {
                            Role = m.Role,
                            TextContent = i == 0 ? m.TextContent : null,
                            ImageContent = imageContent,
                            // Include thought signature for text-only parts (we carry it only when no image).
                            TextThoughtSignature = null,
                        }
                    );
                }
            }
        }
        else
        {
            logger.LogInformation(
                "Skipping conversation history due to incompatible thought signature requirements. "
                    + "Using carry-forward image instead."
            );
        }

        // Build request
        var request = new ImageGenerationRequest
        {
            TextPrompt = textPrompt,
            InputImages = inputImages,
            ConversationHistory = conversationHistory,
            ProviderOptions = providerOptions,
            Progress = progress,
        };

        // Generate response
        logger.LogInformation(
            "Generating image with provider {ProviderId} for conversation {ConversationId}",
            provider.ProviderId,
            conversationId
        );

        progress?.Report(
            new ImageGenerationProgress(
                ProviderId: providerId,
                PromptId: null,
                Value: null,
                Maximum: null,
                RunningNode: null,
                Stage: "Generating..."
            )
        );

        var response = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            logger.LogError("Image generation failed: {ErrorMessage}", response.ErrorMessage);

            // Don't save error messages to the database - let the caller handle the error via UI
            // Update conversation timestamp and provider
            var errorUpdatedConversation = conversation with
            {
                ProviderId = providerId,
                UpdatedAt = DateTime.UtcNow,
            };
            await database.Conversations.UpdateAsync(errorUpdatedConversation).ConfigureAwait(false);

            // Throw exception so caller can handle it appropriately (show notification, etc.)
            throw new ImageGenerationException(response.ErrorMessage ?? "Image generation failed")
            {
                DetailJson = response.ErrorDetailJson,
            };
        }

        // Save generated images
        List<string>? savedImagePaths = null;
        if (response.Images?.Count > 0)
        {
            progress?.Report(
                new ImageGenerationProgress(
                    ProviderId: providerId,
                    PromptId: null,
                    Value: null,
                    Maximum: null,
                    RunningNode: null,
                    Stage: "Saving image(s)..."
                )
            );

            var outputDir = GetOutputDirectory();
            Directory.CreateDirectory(outputDir);

            savedImagePaths = [];

            foreach (var generatedImage in response.Images)
            {
                var imageBytes = Convert.FromBase64String(generatedImage.Base64Data);
                var extension = GetExtensionFromMimeType(generatedImage.MimeType);
                var fileName = CreateOutputFileName(extension);
                var savedPath = Path.Combine(outputDir, fileName);

                await File.WriteAllBytesAsync(savedPath, imageBytes, cancellationToken).ConfigureAwait(false);
                savedImagePaths.Add(savedPath);
            }

            logger.LogInformation(
                "Saved {Count} generated image(s) to {OutputDir}",
                savedImagePaths.Count,
                outputDir
            );
        }

        // Create assistant message - capture thought signature for multi-turn continuity
        // The thought signature comes from either the image part or the response level
        var thoughtSignature =
            response.Images?.FirstOrDefault()?.ThoughtSignature ?? response.ThoughtSignature;

        var primarySavedImagePath = savedImagePaths?.FirstOrDefault();
        var assistantMessage = new ImageGenerationMessage
        {
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            TextContent = response.TextResponse,
            ImagePath = primarySavedImagePath,
            ImagePaths = savedImagePaths,
            ImageMimeType = response.Images?.FirstOrDefault()?.MimeType,
            ThinkingContent = response.ThinkingContent,
            ThoughtSignature = thoughtSignature,
        };

        if (!string.IsNullOrEmpty(thoughtSignature))
        {
            logger.LogInformation("Saved thought signature for assistant message");
        }

        await database.Messages.InsertAsync(assistantMessage).ConfigureAwait(false);

        // Update conversation title if this is the first exchange, and always update timestamp
        var completionUpdate = conversation with
        {
            ProviderId = providerId,
            UpdatedAt = DateTime.UtcNow,
        };

        await database.Conversations.UpdateAsync(completionUpdate).ConfigureAwait(false);

        return (userMessage, assistantMessage);
    }

    public async Task<ImageGenerationMessage> RetryGenerationAsync(
        Guid conversationId,
        string providerId,
        Dictionary<string, object>? providerOptions = null,
        IProgress<ImageGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var conversation = await GetConversationAsync(conversationId).ConfigureAwait(false);
        if (conversation == null)
        {
            throw new InvalidOperationException($"Conversation {conversationId} not found");
        }

        var provider = GetProvider(providerId);
        if (provider == null)
        {
            throw new InvalidOperationException($"Provider {providerId} not found");
        }

        // Get all messages to find the last user message and build history
        var allMessages = await GetMessagesAsync(conversationId).ConfigureAwait(false);

        // Check for provider compatibility - thought signature requirements
        string? carryForwardImagePath = null;
        if (provider.RequiresThoughtSignatures)
        {
            var incompatibleMessages = allMessages
                .Where(m =>
                    m.Role == MessageRole.Assistant
                    && !string.IsNullOrEmpty(m.ImagePath)
                    && string.IsNullOrEmpty(m.ThoughtSignature)
                )
                .ToList();

            if (incompatibleMessages.Count > 0)
            {
                // Find the last assistant message with an image to carry forward
                var lastAssistantImage = allMessages
                    .Where(m => m.Role == MessageRole.Assistant && !string.IsNullOrEmpty(m.ImagePath))
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                if (lastAssistantImage != null)
                {
                    carryForwardImagePath = GetMessageImagePaths(lastAssistantImage)
                        .FirstOrDefault(File.Exists);
                    if (carryForwardImagePath != null)
                    {
                        logger.LogInformation(
                            "Retry: Switching to thinking model with incompatible history. "
                                + "Carrying forward last image as input: {ImagePath}",
                            carryForwardImagePath
                        );
                    }
                }
            }
        }

        // Find the last user message
        var lastUserMessage = allMessages.LastOrDefault(m => m.Role == MessageRole.User);
        if (lastUserMessage == null)
        {
            throw new InvalidOperationException("No user message found to retry");
        }

        // Build conversation history (everything except the last user message)
        // If we're carrying forward an image, skip the incompatible history
        var conversationHistory = new List<ConversationMessage>();

        if (string.IsNullOrEmpty(carryForwardImagePath))
        {
            foreach (var m in allMessages.Where(msg => msg.Id != lastUserMessage.Id))
            {
                var messageImagePaths = GetMessageImagePaths(m).Where(File.Exists).ToList();

                if (messageImagePaths.Count == 0)
                {
                    conversationHistory.Add(
                        new ConversationMessage
                        {
                            Role = m.Role,
                            TextContent = m.TextContent,
                            ImageContent = null,
                            TextThoughtSignature = m.ThoughtSignature,
                        }
                    );
                    continue;
                }

                for (var i = 0; i < messageImagePaths.Count; i++)
                {
                    var imagePath = messageImagePaths[i];
                    ImageInputData? imageContent = null;
                    try
                    {
                        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken)
                            .ConfigureAwait(false);
                        imageContent = new ImageInputData
                        {
                            Base64Data = Convert.ToBase64String(imageBytes),
                            MimeType = GetMimeTypeFromPath(imagePath),
                            FilePath = imagePath,
                            ThoughtSignature = m.ThoughtSignature,
                        };
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to load history image from {ImagePath}", imagePath);
                    }

                    conversationHistory.Add(
                        new ConversationMessage
                        {
                            Role = m.Role,
                            TextContent = i == 0 ? m.TextContent : null,
                            ImageContent = imageContent,
                            TextThoughtSignature = null,
                        }
                    );
                }
            }
        }
        else
        {
            logger.LogInformation(
                "Retry: Skipping conversation history due to incompatible thought signature requirements. "
                    + "Using carry-forward image instead."
            );
        }

        // Build input images from the last user message
        List<ImageInputData>? inputImages = null;

        // If we have a carry-forward image, add it first
        if (!string.IsNullOrEmpty(carryForwardImagePath) && File.Exists(carryForwardImagePath))
        {
            inputImages = [];
            var imageBytes = await File.ReadAllBytesAsync(carryForwardImagePath, cancellationToken)
                .ConfigureAwait(false);
            inputImages.Add(
                new ImageInputData
                {
                    Base64Data = Convert.ToBase64String(imageBytes),
                    MimeType = GetMimeTypeFromPath(carryForwardImagePath),
                    FilePath = carryForwardImagePath,
                }
            );
        }

        var retryImagePaths =
            lastUserMessage.ImagePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()
            ?? (!string.IsNullOrEmpty(lastUserMessage.ImagePath) ? [lastUserMessage.ImagePath] : []);

        retryImagePaths = retryImagePaths.Where(File.Exists).ToList();

        if (retryImagePaths.Count > 0)
        {
            inputImages ??= [];
            foreach (var retryImagePath in retryImagePaths)
            {
                var imageBytes = await File.ReadAllBytesAsync(retryImagePath, cancellationToken)
                    .ConfigureAwait(false);
                inputImages.Add(
                    new ImageInputData
                    {
                        Base64Data = Convert.ToBase64String(imageBytes),
                        MimeType = GetMimeTypeFromPath(retryImagePath),
                        FilePath = retryImagePath,
                    }
                );
            }
        }

        // Build request
        var request = new ImageGenerationRequest
        {
            TextPrompt = lastUserMessage.TextContent,
            InputImages = inputImages,
            ConversationHistory = conversationHistory,
            ProviderOptions = providerOptions,
            Progress = progress,
        };

        logger.LogInformation(
            "Retrying generation with provider {ProviderId} for conversation {ConversationId}",
            provider.ProviderId,
            conversationId
        );

        progress?.Report(
            new ImageGenerationProgress(
                ProviderId: providerId,
                PromptId: null,
                Value: null,
                Maximum: null,
                RunningNode: null,
                Stage: "Generating..."
            )
        );

        var response = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            logger.LogError("Retry generation failed: {ErrorMessage}", response.ErrorMessage);

            // Update conversation provider and timestamp
            conversation.ProviderId = providerId;
            conversation.UpdatedAt = DateTime.UtcNow;
            await database.Conversations.UpdateAsync(conversation).ConfigureAwait(false);

            throw new ImageGenerationException(response.ErrorMessage ?? "Image generation failed")
            {
                DetailJson = response.ErrorDetailJson,
            };
        }

        // Save generated images
        List<string>? savedImagePaths = null;
        if (response.Images?.Count > 0)
        {
            progress?.Report(
                new ImageGenerationProgress(
                    ProviderId: providerId,
                    PromptId: null,
                    Value: null,
                    Maximum: null,
                    RunningNode: null,
                    Stage: "Saving image(s)..."
                )
            );

            var outputDir = GetOutputDirectory();
            Directory.CreateDirectory(outputDir);

            savedImagePaths = [];

            foreach (var generatedImage in response.Images)
            {
                var imageBytes = Convert.FromBase64String(generatedImage.Base64Data);
                var extension = GetExtensionFromMimeType(generatedImage.MimeType);
                var fileName = CreateOutputFileName(extension);
                var savedPath = Path.Combine(outputDir, fileName);

                await File.WriteAllBytesAsync(savedPath, imageBytes, cancellationToken).ConfigureAwait(false);
                savedImagePaths.Add(savedPath);
            }

            logger.LogInformation(
                "Saved {Count} retry generated image(s) to {OutputDir}",
                savedImagePaths.Count,
                outputDir
            );
        }

        // Create assistant message - capture thought signature for multi-turn continuity
        var thoughtSignature =
            response.Images?.FirstOrDefault()?.ThoughtSignature ?? response.ThoughtSignature;

        var assistantMessage = new ImageGenerationMessage
        {
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            TextContent = response.TextResponse,
            ImagePath = savedImagePaths?.FirstOrDefault(),
            ImagePaths = savedImagePaths,
            ImageMimeType = response.Images?.FirstOrDefault()?.MimeType,
            ThinkingContent = response.ThinkingContent,
            ThoughtSignature = thoughtSignature,
        };

        if (!string.IsNullOrEmpty(thoughtSignature))
        {
            logger.LogInformation("Saved thought signature for retry assistant message");
        }

        await database.Messages.InsertAsync(assistantMessage).ConfigureAwait(false);

        // Update conversation
        conversation.ProviderId = providerId;
        conversation.UpdatedAt = DateTime.UtcNow;
        await database.Conversations.UpdateAsync(conversation).ConfigureAwait(false);

        return assistantMessage;
    }

    public List<IImageGenerationProvider> GetAvailableProviders()
    {
        return providers;
    }

    public IImageGenerationProvider? GetProvider(string providerId)
    {
        return providers.FirstOrDefault(p => p.ProviderId == providerId);
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
