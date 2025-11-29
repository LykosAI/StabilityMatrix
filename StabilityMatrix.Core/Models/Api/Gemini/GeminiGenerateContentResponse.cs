using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Gemini;

/// <summary>
/// Response from Gemini generateContent endpoint
/// </summary>
public record GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; init; }
}

/// <summary>
/// A candidate response
/// </summary>
public record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }
}

/// <summary>
/// Safety rating for generated content
/// </summary>
public record GeminiSafetyRating
{
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("probability")]
    public string? Probability { get; init; }
}

/// <summary>
/// Feedback about the prompt
/// </summary>
public record GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; init; }

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }
}
