using System.Text.Json.Serialization;
using StabilityMatrix.Converters.Json;

namespace StabilityMatrix.Updater;

[JsonConverter(typeof(DefaultUnknownEnumConverter<UpdateChannel>))]
public enum UpdateChannel
{
    Unknown,
    Stable,
    Preview,
    Development
}
