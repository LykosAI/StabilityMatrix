using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Gemini;

namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Image generation provider for Google Gemini (Nano Banana)
/// </summary>
public class GeminiImageGenerationProvider(
    ILogger<GeminiImageGenerationProvider> logger,
    IGeminiApi geminiApi,
    ISecretsManager secretsManager
) : GeminiBaseImageGenerationProvider(logger, geminiApi, secretsManager)
{
    public override string ProviderId => BananaVisionProviderIds.Gemini25Flash;
    public override string ProviderName => "Gemini 2.5 Flash (Nano Banana)";
    public override string DefaultModel => "gemini-2.5-flash-image";
}
