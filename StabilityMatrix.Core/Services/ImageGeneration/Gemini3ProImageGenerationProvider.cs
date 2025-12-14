using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Gemini;

namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Image generation provider for Google Gemini 3 Pro (Nano Banana Pro Preview)
/// with thinking/reasoning support
/// </summary>
public class Gemini3ProImageGenerationProvider(
    ILogger<Gemini3ProImageGenerationProvider> logger,
    IGeminiApi geminiApi,
    ISecretsManager secretsManager
) : GeminiBaseImageGenerationProvider(logger, geminiApi, secretsManager)
{
    private const int DefaultThinkingBudget = 2048;

    public override string ProviderId => BananaVisionProviderIds.Gemini3Pro;
    public override string ProviderName => "Gemini 3 Pro (Nano Banana Pro)";
    public override string DefaultModel => "gemini-3-pro-image-preview";
    public override bool RequiresThoughtSignatures => true;

    protected override GeminiGenerateContentRequest BuildGeminiRequest(ImageGenerationRequest request)
    {
        // Get the base request
        var geminiRequest = base.BuildGeminiRequest(request);

        // Check if thinking is enabled
        var enableThinking =
            request.ProviderOptions?.TryGetValue("enableThinking", out var thinkingValue) == true
            && thinkingValue is true or "true";

        var thinkingBudget =
            request.ProviderOptions?.TryGetValue("thinkingBudget", out var budgetValue) == true
            && budgetValue is int budget
                ? budget
                : DefaultThinkingBudget;

        Logger.LogInformation(
            "Gemini 3 Pro Config - Thinking: {Thinking}, Budget: {Budget}",
            enableThinking,
            enableThinking ? thinkingBudget : 0
        );

        // Add thinking config if enabled
        if (enableThinking)
        {
            var existingConfig = geminiRequest.GenerationConfig ?? new GeminiGenerationConfig();

            geminiRequest = geminiRequest with
            {
                GenerationConfig = existingConfig with
                {
                    ThinkingConfig = new GeminiThinkingConfig
                    {
                        ThinkingBudget = thinkingBudget,
                        IncludeThoughts = true,
                    },
                },
            };
        }

        return geminiRequest;
    }

    protected override ImageGenerationResponse ParseGeminiResponse(GeminiGenerateContentResponse response)
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
            var parts = candidate.Content.Parts;

            // Find the index of the last part that has any text content (thinking or regular)
            // Images after this index are considered "final" outputs
            // Images before this are considered intermediate/draft images from the thinking process
            var lastTextPartIndex = -1;
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                // Count any part with non-empty text (including thinking parts)
                // Image parts often have Text = "" which we ignore
                if (!string.IsNullOrEmpty(part.Text))
                {
                    lastTextPartIndex = i;
                }
            }

            Logger.LogDebug(
                "Gemini 3 Pro response has {PartCount} parts, last text part at index {LastTextIndex}",
                parts.Count,
                lastTextPartIndex
            );

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];

                // Capture thought signature from any part that has one
                if (!string.IsNullOrEmpty(part.ThoughtSignature))
                {
                    lastThoughtSignature = part.ThoughtSignature;
                }

                // Check for thinking content
                if (part is { Thought: true, Text: not null })
                {
                    thinkingContent = string.IsNullOrEmpty(thinkingContent)
                        ? part.Text
                        : thinkingContent + "\n\n" + part.Text;
                    continue;
                }

                if (!string.IsNullOrEmpty(part.Text))
                {
                    textResponse = part.Text;
                }

                if (part.InlineData != null)
                {
                    // Only include images that appear at or after the last text part
                    // This filters out intermediate "thinking" images that appear between text parts
                    if (i >= lastTextPartIndex)
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
                    else
                    {
                        Logger.LogDebug(
                            "Skipping intermediate image at index {Index} (before last text at {LastTextIndex})",
                            i,
                            lastTextPartIndex
                        );
                    }
                }
            }
        }

        var responseThoughtSignature = images.FirstOrDefault()?.ThoughtSignature ?? lastThoughtSignature;

        Logger.LogInformation(
            "Gemini 3 Pro parsed response: {ImageCount} final image(s), has thinking: {HasThinking}",
            images.Count,
            !string.IsNullOrEmpty(thinkingContent)
        );

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
