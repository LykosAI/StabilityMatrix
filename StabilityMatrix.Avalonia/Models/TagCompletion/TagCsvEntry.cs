namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public record TagCsvEntry
{
    public string? Name { get; init; }
    
    public int? Type { get; init; }
    
    public int? Count { get; init; }
    
    public string? Aliases { get; init; }
}
