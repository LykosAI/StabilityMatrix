using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Processes;

[JsonConverter(typeof(JsonStringEnumConverter<ApcType>))]
public enum ApcType
{
    [EnumMember(Value = "input")]
    Input = 1,
}
