using System;
using System.Collections.Generic;

namespace StabilityMatrix.Models;

/// <summary>
/// Defines a launch option for a BasePackage.
/// </summary>
public class LaunchOptionDefinition
{
    public string Name { get; set; }

    /// <summary>
    /// Type of the option. "bool", "int", or "string"
    /// - "bool" can supply 1 or more flags in the Options list (e.g. ["--api", "--lowvram"])
    /// - "int" and "string" should supply a single flag in the Options list (e.g. ["--width"], ["--api"])
    /// </summary>
    public string Type { get; set; } = "bool";
    public string? Description { get; set; }
    
    /// <summary>
    /// Constant default value for the option.
    /// </summary>
    public object? DefaultValue { get; set; }

    // Minimum number of selected options
    public int? MinSelectedOptions { get; set; }
    // Maximum number of selected options
    public int? MaxSelectedOptions { get; set; }

    /// <summary>
    /// List of option flags like "--api", "--lowvram", etc.
    /// </summary>
    public List<string> Options { get; set; }
}
