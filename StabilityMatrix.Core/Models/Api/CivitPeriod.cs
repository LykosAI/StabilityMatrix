using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter<CivitPeriod>))]
public enum CivitPeriod
{
    AllTime,
    Year,
    Month,
    Week,
    Day
}
