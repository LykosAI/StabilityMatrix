using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Defines a launch option for a BasePackage.
/// </summary>
public record LaunchOptionDefinition
{
    /// <summary>
    /// Name or title of the card.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type of the option. "bool", "int", or "string"
    /// - "bool" can supply 1 or more flags in the Options list (e.g. ["--api", "--lowvram"])
    /// - "int" and "string" should supply a single flag in the Options list (e.g. ["--width"], ["--api"])
    /// </summary>
    public required LaunchOptionType Type { get; init; }
    
    /// <summary>
    /// Optional description of the option.
    /// </summary>
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
    public object? InitialValue { get; init; }

    /// <summary>
    /// Minimum number of selected options (for validation)
    /// </summary>
    public int? MinSelectedOptions { get; init; }
    
    /// <summary>
    /// Maximum number of selected options (for validation)
    /// </summary>
    public int? MaxSelectedOptions { get; init; }

    /// <summary>
    /// List of option flags like "--api", "--lowvram", etc.
    /// </summary>
    public List<string> Options { get; init; } = new();
    
    /// <summary>
    /// Constant for the Extras launch option.
    /// </summary>
    [JsonIgnore]
    public static LaunchOptionDefinition Extras => new()
    {
        Name = "Extra Launch Arguments",
        Type = LaunchOptionType.String,
        Options = new List<string> {""}
    };
}
