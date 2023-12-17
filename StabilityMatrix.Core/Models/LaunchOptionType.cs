using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<LaunchOptionType>))]
public enum LaunchOptionType
{
    Bool,
    String,
    Int
}
