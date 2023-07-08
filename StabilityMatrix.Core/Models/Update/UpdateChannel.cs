using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Update;

[JsonConverter(typeof(DefaultUnknownEnumConverter<UpdateChannel>))]
public enum UpdateChannel
{
    Unknown,
    Stable,
    Preview,
    Development
}
