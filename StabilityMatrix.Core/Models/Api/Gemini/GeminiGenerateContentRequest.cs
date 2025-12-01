using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Gemini;

/// <summary>
/// Request for Gemini generateContent endpoint
/// </summary>
public record GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; init; } = new();

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }
}

/// <summary>
/// Content part of the request (can be from user or model)
/// </summary>
public record GeminiContent
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; init; } = new();
}

/// <summary>
/// Individual part of content (text or image)
/// </summary>
public record GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("inlineData")]
    public GeminiInlineData? InlineData { get; init; }

    /// <summary>
    /// Whether this part contains thinking/reasoning content (response only)
    /// </summary>
    [JsonPropertyName("thought")]
    public bool? Thought { get; init; }

    /// <summary>
    /// Encrypted representation of the model's internal thought process.
    /// Must be captured from responses and passed back in follow-up requests
    /// to preserve reasoning context across multi-turn interactions.
    /// See: https://ai.google.dev/gemini-api/docs/thought-signatures
    /// </summary>
    [JsonPropertyName("thoughtSignature")]
    public string? ThoughtSignature { get; init; }
}

/// <summary>
/// Inline data for images
/// </summary>
public record GeminiInlineData
{
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }

    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

/// <summary>
/// Generation configuration options
/// </summary>
public record GeminiGenerationConfig
{
    [JsonPropertyName("responseModalities")]
    public List<string>? ResponseModalities { get; init; }

    [JsonPropertyName("imageConfig")]
    public GeminiImageConfig? ImageConfig { get; init; }

    /// <summary>
    /// Configuration for thinking/reasoning (Gemini 3 Pro)
    /// </summary>
    [JsonPropertyName("thinkingConfig")]
    public GeminiThinkingConfig? ThinkingConfig { get; init; }
}

/// <summary>
/// Image-specific configuration
/// </summary>
public record GeminiImageConfig
{
    [JsonPropertyName("aspectRatio")]
    public string? AspectRatio { get; init; }
}

/// <summary>
/// Configuration for thinking/reasoning output (Gemini 3 Pro)
/// </summary>
public record GeminiThinkingConfig
{
    /// <summary>
    /// The thinking budget in tokens. Set to 0 to disable thinking.
    /// Recommended values: 1024-8192 for complex tasks.
    /// </summary>
    [JsonPropertyName("thinkingBudget")]
    public int? ThinkingBudget { get; init; }

    /// <summary>
    /// Whether to include thinking content in the response
    /// </summary>
    [JsonPropertyName("includeThoughts")]
    public bool? IncludeThoughts { get; init; }
}
