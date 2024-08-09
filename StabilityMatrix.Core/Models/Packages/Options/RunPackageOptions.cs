namespace StabilityMatrix.Core.Models.Packages;

public class RunPackageOptions
{
    public string? Command { get; set; }

    public IReadOnlyList<string> Arguments { get; set; } = Array.Empty<string>();
}
