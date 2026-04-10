namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Progress update emitted during image generation.
/// Intended to be UI-friendly and provider-agnostic.
/// </summary>
public readonly record struct ImageGenerationProgress(
    string? ProviderId,
    string? PromptId,
    int? Value,
    int? Maximum,
    string? RunningNode,
    string? Stage
)
{
    public int? Percent => Value is >= 0 && Maximum is > 0 ? (Value.Value * 100) / Maximum.Value : null;
}
