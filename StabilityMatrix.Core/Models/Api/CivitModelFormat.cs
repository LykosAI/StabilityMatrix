using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CivitModelFormat
{
    SafeTensor,
    PickleTensor,
    Other
}
