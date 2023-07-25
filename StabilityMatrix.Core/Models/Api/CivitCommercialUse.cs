using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CivitCommercialUse
{
    None,
    Image,
    Rent,
    Sell
}
