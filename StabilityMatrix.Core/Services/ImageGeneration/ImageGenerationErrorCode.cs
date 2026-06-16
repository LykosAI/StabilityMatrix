namespace StabilityMatrix.Core.Services.ImageGeneration;

public enum ImageGenerationErrorCode
{
    GeminiApiKeyNotConfigured,
    GeminiQuotaExceeded,
    GeminiInvalidApiKey,
    GeminiAccessForbidden,
}
