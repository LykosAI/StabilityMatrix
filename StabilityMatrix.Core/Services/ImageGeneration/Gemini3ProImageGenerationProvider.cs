using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Gemini;

namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Image generation provider for Google Gemini 3 Pro (Nano Banana Pro Preview)
/// with thinking/reasoning support
/// </summary>
public class Gemini3ProImageGenerationProvider(
    ILogger<Gemini3ProImageGenerationProvider> logger,
    IGeminiApi geminiApi
) : IImageGenerationProvider
{
    private const string DefaultModel = "gemini-3-pro-image-preview";
    private const int DefaultThinkingBudget = 2048;

    public string ProviderId => BananaVisionProviderIds.Gemini3Pro;
    public string ProviderName => "Gemini 3 Pro (Nano Banana Pro)";
    public bool SupportsImageInput => true;
    public bool SupportsMultiTurn => true;

    public async Task<ImageGenerationResponse> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Check if thinking is enabled
            var enableThinking =
                request.ProviderOptions?.TryGetValue("enableThinking", out var thinkingValue) == true
                && thinkingValue is true or "true";

            var thinkingBudget =
                request.ProviderOptions?.TryGetValue("thinkingBudget", out var budgetValue) == true
                && budgetValue is int budget
                    ? budget
                    : DefaultThinkingBudget;

            var geminiRequest = BuildGeminiRequest(request, enableThinking, thinkingBudget);

            var model =
                request.ProviderOptions?.TryGetValue("model", out var modelValue) == true
                    ? modelValue?.ToString() ?? DefaultModel
                    : DefaultModel;

            logger.LogInformation(
                "Generating image with Gemini 3 Pro model: {Model}, Thinking: {Thinking}, Budget: {Budget}",
                model,
                enableThinking,
                enableThinking ? thinkingBudget : 0
            );

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
            logger.LogError(ex, "Failed to generate image with Gemini 3 Pro");
            return new ImageGenerationResponse { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private GeminiGenerateContentRequest BuildGeminiRequest(
        ImageGenerationRequest request,
        bool enableThinking,
        int thinkingBudget
    )
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
                    // Include thought signature on text parts if available (for model responses)
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
                    // Include thought signature on image parts if available
                    // This is critical for Gemini 3 Pro multi-turn conversations
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

        // Build generation config with thinking support
        var generationConfig = new GeminiGenerationConfig
        {
            ResponseModalities = ["TEXT", "IMAGE"],
            ThinkingConfig = enableThinking
                ? new GeminiThinkingConfig { ThinkingBudget = thinkingBudget, IncludeThoughts = true }
                : null,
        };

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

    private ImageGenerationResponse ParseGeminiResponse(GeminiGenerateContentResponse response)
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
                // According to Gemini docs, for non-function-call responses,
                // the signature is on the last part if the model generates a thought
                if (!string.IsNullOrEmpty(part.ThoughtSignature))
                {
                    lastThoughtSignature = part.ThoughtSignature;
                    logger.LogDebug("Captured thought signature from response part");
                }

                // Check if this is thinking content
                if (part is { Thought: true, Text: not null })
                {
                    // Accumulate thinking content
                    thinkingContent = string.IsNullOrEmpty(thinkingContent)
                        ? part.Text
                        : thinkingContent + "\n\n" + part.Text;
                    continue;
                }

                // Regular text response
                if (part.Text != null)
                {
                    textResponse = part.Text;
                }

                // Image response - capture thought signature for this specific image
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

        // Use the last thought signature found if no image-specific one
        var responseThoughtSignature = images.FirstOrDefault()?.ThoughtSignature ?? lastThoughtSignature;

        if (!string.IsNullOrEmpty(responseThoughtSignature))
        {
            logger.LogInformation("Captured thought signature for conversation continuity");
        }

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
