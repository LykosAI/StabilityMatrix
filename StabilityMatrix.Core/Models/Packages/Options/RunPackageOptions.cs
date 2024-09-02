using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.Packages;

public class RunPackageOptions
{
    public string? Command { get; set; }

    public ProcessArgs Arguments { get; set; } = [];
}
