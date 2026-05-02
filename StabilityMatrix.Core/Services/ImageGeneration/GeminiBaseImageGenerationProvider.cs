using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Gemini;

namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Base class for Gemini image generation providers
/// </summary>
public abstract class GeminiBaseImageGenerationProvider(
    ILogger logger,
    IGeminiApi geminiApi,
    ISecretsManager secretsManager
) : IImageGenerationProvider
{
    public abstract string ProviderId { get; }
    public abstract string ProviderName { get; }
    public abstract string DefaultModel { get; }
    public bool SupportsImageInput => true;
    public bool SupportsMultiTurn => true;
    public virtual bool RequiresThoughtSignatures => false;

    protected ILogger Logger => logger;

    public async Task<ImageGenerationResponse> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        // Check for API key first
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(secrets.GeminiApiKey))
        {
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage = "Gemini API key not configured. Please add it in Settings.",
            };
        }

        try
        {
            var geminiRequest = BuildGeminiRequest(request);

            var model =
                request.ProviderOptions?.TryGetValue("model", out var modelValue) == true
                    ? modelValue?.ToString() ?? DefaultModel
                    : DefaultModel;

            logger.LogInformation("Generating image with Gemini model: {Model}", model);

            var response = await geminiApi
                .GenerateContentAsync(model, geminiRequest, cancellationToken)
                .ConfigureAwait(false);

            return ParseGeminiResponse(response);
        }
        catch (ApiException apiEx) when (apiEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            logger.LogError(apiEx, "Rate limit or quota exceeded for Gemini API");
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage =
                    "Rate limit or quota exceeded. "
                    + "Note: Free Gemini API keys do not support image generation - you need a paid API key. "
                    + "If you have a paid key, you may be hitting rate limits. Please try again in a moment.",
            };
        }
        catch (ApiException apiEx)
        {
            logger.LogError(apiEx, "Gemini API error: {StatusCode}", apiEx.StatusCode);

            var errorMessage = apiEx.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized =>
                    "Invalid API key. Please check your Gemini API key in Settings.",
                System.Net.HttpStatusCode.Forbidden =>
                    "Access forbidden. Your API key may not have the required permissions.",
                System.Net.HttpStatusCode.BadRequest => $"Invalid request: {apiEx.Content}",
                _ => $"API error ({apiEx.StatusCode}): {apiEx.Message}",
            };

            return new ImageGenerationResponse { IsSuccess = false, ErrorMessage = errorMessage };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate image with Gemini");
            return new ImageGenerationResponse { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Builds the Gemini request. Can be overridden to add specific configuration.
    /// </summary>
    protected virtual GeminiGenerateContentRequest BuildGeminiRequest(ImageGenerationRequest request)
    {
        var contents = new List<GeminiContent>();

        // Add conversation history if present
        if (request.ConversationHistory != null)
        {
            foreach (var message in request.ConversationHistory)
            {
                var parts = new List<GeminiPart>();

                if (!string.IsNullOrEmpty(message.TextContent))
                {
                    parts.Add(
                        new GeminiPart
                        {
                            Text = message.TextContent,
                            ThoughtSignature = message.TextThoughtSignature,
                        }
                    );
                }

                if (message.ImageContent != null)
                {
                    parts.Add(
                        new GeminiPart
                        {
                            InlineData = new GeminiInlineData
                            {
                                MimeType = message.ImageContent.MimeType,
                                Data = message.ImageContent.Base64Data,
                            },
                            ThoughtSignature = message.ImageContent.ThoughtSignature,
                        }
                    );
                }

                if (parts.Count > 0)
                {
                    contents.Add(
                        new GeminiContent
                        {
                            Role = message.Role == MessageRole.User ? "user" : "model",
                            Parts = parts,
                        }
                    );
                }
            }
        }

        // Add current request
        var currentParts = new List<GeminiPart>();

        if (!string.IsNullOrEmpty(request.TextPrompt))
        {
            currentParts.Add(new GeminiPart { Text = request.TextPrompt });
        }

        if (request.InputImages != null)
        {
            foreach (var image in request.InputImages)
            {
                currentParts.Add(
                    new GeminiPart
                    {
                        InlineData = new GeminiInlineData
                        {
                            MimeType = image.MimeType,
                            Data = image.Base64Data,
                        },
                    }
                );
            }
        }

        if (currentParts.Count > 0)
        {
            contents.Add(new GeminiContent { Role = "user", Parts = currentParts });
        }

        // Build generation config
        var generationConfig = new GeminiGenerationConfig { ResponseModalities = ["TEXT", "IMAGE"] };

        // Add aspect ratio if specified
        if (request.ProviderOptions?.TryGetValue("aspectRatio", out var aspectRatioValue) == true)
        {
            generationConfig = generationConfig with
            {
                ImageConfig = new GeminiImageConfig { AspectRatio = aspectRatioValue?.ToString() },
            };
        }

        return new GeminiGenerateContentRequest { Contents = contents, GenerationConfig = generationConfig };
    }

    protected virtual ImageGenerationResponse ParseGeminiResponse(GeminiGenerateContentResponse response)
    {
        if (response.Candidates == null || response.Candidates.Count == 0)
        {
            var blockReason = response.PromptFeedback?.BlockReason;
            return new ImageGenerationResponse
            {
                IsSuccess = false,
                ErrorMessage = string.IsNullOrEmpty(blockReason)
                    ? "No candidates returned from Gemini"
                    : $"Request blocked: {blockReason}",
            };
        }

        var candidate = response.Candidates[0];
        var images = new List<GeneratedImage>();
        string? textResponse = null;
        string? thinkingContent = null;
        string? lastThoughtSignature = null;

        if (candidate.Content?.Parts != null)
        {
            foreach (var part in candidate.Content.Parts)
            {
                // Capture thought signature from any part that has one
                if (!string.IsNullOrEmpty(part.ThoughtSignature))
                {
                    lastThoughtSignature = part.ThoughtSignature;
                }

                // Check for thinking content (Gemini 3 Pro)
                if (part is { Thought: true, Text: not null })
                {
                    thinkingContent = string.IsNullOrEmpty(thinkingContent)
                        ? part.Text
                        : thinkingContent + "\n\n" + part.Text;
                    continue;
                }

                if (part.Text != null)
                {
                    textResponse = part.Text;
                }

                if (part.InlineData != null)
                {
                    images.Add(
                        new GeneratedImage
                        {
                            Base64Data = part.InlineData.Data,
                            MimeType = part.InlineData.MimeType,
                            ThoughtSignature = part.ThoughtSignature,
                        }
                    );
                }
            }
        }

        var responseThoughtSignature = images.FirstOrDefault()?.ThoughtSignature ?? lastThoughtSignature;

        return new ImageGenerationResponse
        {
            IsSuccess = true,
            Images = images.Count > 0 ? images : null,
            TextResponse = textResponse,
            ThinkingContent = thinkingContent,
            ThoughtSignature = responseThoughtSignature,
            Metadata = new Dictionary<string, object>
            {
                ["finishReason"] = candidate.FinishReason ?? "unknown",
                ["hasThinking"] = !string.IsNullOrEmpty(thinkingContent),
            },
        };
    }
}
