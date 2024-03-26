using System.Collections.Generic;

namespace StabilityMatrix.Avalonia.Models;

public class OpenArtCustomNode
{
    public required string Title { get; set; }
    public List<string> Children { get; set; } = [];
    public bool IsInstalled { get; set; }
}
