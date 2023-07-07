using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models;

public class LaunchOption
{
    public string Name { get; init; }
    
    public LaunchOptionType Type { get; init; }
    
    [JsonIgnore]
    public object? DefaultValue { get; init; }
    
    [JsonIgnore]
    public bool HasDefaultValue => DefaultValue != null;

    [JsonConverter(typeof(LaunchOptionValueJsonConverter))]
    public object? OptionValue { get; set; }

    public bool IsEmptyOrDefault()
    {
        switch (Type)
        {
            case LaunchOptionType.Bool:
                return OptionValue == null;
            case LaunchOptionType.Int:
                return OptionValue == null || (int?) OptionValue == (int?) DefaultValue;
            case LaunchOptionType.String:
                return OptionValue == null || (string?) OptionValue == (string?) DefaultValue;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SetValueFromString(string? value)
    {
        OptionValue = Type switch
        {
            LaunchOptionType.Bool => bool.TryParse(value, out var boolValue) ? boolValue : null,
            LaunchOptionType.Int => int.TryParse(value, out var intValue) ? intValue : null,
            LaunchOptionType.String => value,
            _ => throw new ArgumentException($"Unknown option type {Type}")
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
