using Refit;
using StabilityMatrix.Core.Models.Api.Gemini;

namespace StabilityMatrix.Core.Api;

/// <summary>
/// Refit interface for Google Gemini API
/// </summary>
[Headers("Content-Type: application/json")]
public interface IGeminiApi
{
    /// <summary>
    /// Generate content (text and/or images) using the specified model
    /// </summary>
    [Post("/v1beta/models/{model}:generateContent")]
    Task<GeminiGenerateContentResponse> GenerateContentAsync(
        [AliasAs("model")] string model,
        [Body] GeminiGenerateContentRequest request,
        CancellationToken cancellationToken = default
    );
}
