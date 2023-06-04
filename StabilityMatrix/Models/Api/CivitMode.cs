using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CivitMode
{
    Archived,
    TakenDown
}
