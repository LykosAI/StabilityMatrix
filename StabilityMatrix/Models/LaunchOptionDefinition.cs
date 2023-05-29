using System.Collections.Generic;

namespace StabilityMatrix.Models;

/// <summary>
/// Defines a launch option for a BasePackage.
/// </summary>
public class LaunchOptionDefinition
{
    public string Name { get; set; }
    // Minimum number of selected options
    public int? MinSelectedOptions { get; set; }
    // Maximum number of selected options
    public int? MaxSelectedOptions { get; set; }
    // List of option flags like "--api", "--lowvram", etc.
    public List<string> Options { get; set; }
}
