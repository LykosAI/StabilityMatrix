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
}
