using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models;

/// <summary>
/// Defines a launch option for a BasePackage.
/// </summary>
public class LaunchOptionDefinition
{
    public string Name { get; init; }

    /// <summary>
    /// Type of the option. "bool", "int", or "string"
    /// - "bool" can supply 1 or more flags in the Options list (e.g. ["--api", "--lowvram"])
    /// - "int" and "string" should supply a single flag in the Options list (e.g. ["--width"], ["--api"])
    /// </summary>
    public LaunchOptionType Type { get; init; } = LaunchOptionType.Bool;
    public string? Description { get; init; }
    
    /// <summary>
    /// Server-side default for the option. (Ignored for launch and saving if value matches)
    /// Use `InitialValue` to provide a default that is set as the user value and used for launch.
    /// </summary>
    public object? DefaultValue { get; init; }
    
    /// <summary>
    /// Initial value for the option if no set value is available, set as the user value on save.
    /// Use `DefaultValue` to provide a server-side default that is ignored for launch and saving.
    /// </summary>
    public object? InitialValue { get; set; }

    // Minimum number of selected options
    public int? MinSelectedOptions { get; set; }
    // Maximum number of selected options
    public int? MaxSelectedOptions { get; set; }

    /// <summary>
    /// List of option flags like "--api", "--lowvram", etc.
    /// </summary>
    public List<string> Options { get; set; }
    
    [JsonIgnore]
    public static LaunchOptionDefinition Extras => new()
    {
        Name = "Extra Launch Arguments",
        Type = LaunchOptionType.String,
        Options = new() {""}
    };
}
