using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter<CivitMode>))]
public enum CivitMode
{
    Archived,
    TakenDown
}
