using Avalonia.Media;

namespace StabilityMatrix.Avalonia.Models;

public record IconData
{
    public string? FAIcon { get; init; }
    
    public int? FontSize { get; init; }
    
    public SolidColorBrush? Foreground { get; init; }
}
