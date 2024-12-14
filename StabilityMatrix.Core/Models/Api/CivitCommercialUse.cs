using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter<CivitCommercialUse>))]
public enum CivitCommercialUse
{
    None,
    Image,
    Rent,
    Sell
}
