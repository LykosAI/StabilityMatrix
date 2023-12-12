using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Settings;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HolidayMode
{
    Automatic,
    Enabled,
    Disabled
}
