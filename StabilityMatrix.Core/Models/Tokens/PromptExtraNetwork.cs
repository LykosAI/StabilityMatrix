namespace StabilityMatrix.Core.Models.Tokens;

/// <summary>
/// Represents an extra network token in a prompt.
/// In format 
/// </summary>
public record PromptExtraNetwork
{
    public required PromptExtraNetworkType Type { get; init; }
    public required string Name { get; init; }
    public double? ModelWeight { get; init; }
    public double? ClipWeight { get; init; }
}
