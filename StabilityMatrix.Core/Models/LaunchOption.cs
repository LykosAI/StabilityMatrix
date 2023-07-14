using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models;

public class LaunchOption
{
    public required string Name { get; init; }

    public LaunchOptionType Type { get; init; } = LaunchOptionType.Bool;
    
    [JsonIgnore]
    public object? DefaultValue { get; init; }
    
    [JsonIgnore]
    public bool HasDefaultValue => DefaultValue != null;

    [JsonConverter(typeof(LaunchOptionValueJsonConverter))]
    public object? OptionValue { get; set; }

    /// <summary>
    /// Checks if the option has no user entered value,
    /// or that the value is the same as the default value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public bool IsEmptyOrDefault()
    {
        return Type switch
        {
            LaunchOptionType.Bool => OptionValue == null,
            LaunchOptionType.Int => OptionValue == null ||
                                    (int?) OptionValue == (int?) DefaultValue,
            LaunchOptionType.String => OptionValue == null ||
                                       (string?) OptionValue == (string?) DefaultValue,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Parses a string value to the correct type for the option.
    /// This returned object can be assigned to OptionValue.
    /// </summary>
    public static object? ParseValue(string? value, LaunchOptionType type)
    {
        return type switch
        {
            LaunchOptionType.Bool => bool.TryParse(value, out var boolValue) ? boolValue : null,
            LaunchOptionType.Int => int.TryParse(value, out var intValue) ? intValue : null,
            LaunchOptionType.String => value,
            _ => throw new ArgumentException($"Unknown option type {type}")
        };
    }

    /// <summary>
    /// Convert the option value to a string that can be passed to a Process.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public string? ToArgString()
    {
        // Convert to string
        switch (Type)
        {
            case LaunchOptionType.Bool:
                return (bool?) OptionValue == true ? Name : null;
            case LaunchOptionType.Int:
                return (int?) OptionValue != null ? $"{Name} {OptionValue}" : null;
            case LaunchOptionType.String:
                var valueString = (string?) OptionValue;
                // Special case empty string name to not do quoting (for custom launch args)
                if (Name == "")
                {
                    return valueString;
                }
                return string.IsNullOrWhiteSpace(valueString) ? null : $"{Name} {ProcessRunner.Quote(valueString)}";
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
