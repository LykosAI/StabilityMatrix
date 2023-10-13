namespace StabilityMatrix.Avalonia.Models.Inference;

public record FileNameFormatVar
{
    public required string Variable { get; init; }

    public string? Example { get; init; }
}
