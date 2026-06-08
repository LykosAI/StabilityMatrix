using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Gemini;

namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Image generation provider for Google Gemini 3.1 Flash (Nano Banana 2)
/// with thinking/reasoning support. Uses the newer thinking_level string config
/// instead of the integer thinking_budget used by Gemini 3 Pro.
/// </summary>
public class Gemini31FlashImageGenerationProvider(
    ILogger<Gemini31FlashImageGenerationProvider> logger,
    IGeminiApi geminiApi,
    ISecretsManager secretsManager
) : GeminiBaseImageGenerationProvider(logger, geminiApi, secretsManager)
{
    private const string DefaultThinkingLevel = "high";

    public override string ProviderId => BananaVisionProviderIds.Gemini31Flash;
    public override string ProviderName => "Gemini 3.1 Flash (Nano Banana 2)";
    public override string DefaultModel => "gemini-3.1-flash-image-preview";
    public override bool RequiresThoughtSignatures => true;

    protected override GeminiGenerateContentRequest BuildGeminiRequest(ImageGenerationRequest request)
    {
        var geminiRequest = base.BuildGeminiRequest(request);

        var enableThinking =
            request.ProviderOptions?.TryGetValue("enableThinking", out var thinkingValue) == true
            && thinkingValue is true or "true";

        var thinkingLevel =
            request.ProviderOptions?.TryGetValue("thinkingLevel", out var levelValue) == true
            && levelValue is string level
            && !string.IsNullOrWhiteSpace(level)
                ? level
                : DefaultThinkingLevel;

        Logger.LogInformation(
            "Gemini 3.1 Flash Config - Thinking: {Thinking}, Level: {Level}",
            enableThinking,
            enableThinking ? thinkingLevel : "minimal"
        );

        // Only attach a thinkingConfig when the user explicitly enabled thinking.
        // Omitting it lets Gemini 3.1 Flash use its server-side default ("minimal").
        if (enableThinking)
        {
            var existingConfig = geminiRequest.GenerationConfig ?? new GeminiGenerationConfig();

            geminiRequest = geminiRequest with
            {
                GenerationConfig = existingConfig with
                {
                    ThinkingConfig = new GeminiThinkingConfig
                    {
                        ThinkingLevel = thinkingLevel,
                        IncludeThoughts = true,
                    },
                },
            };
        }

        return geminiRequest;
    }
}
