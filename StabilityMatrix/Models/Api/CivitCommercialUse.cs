using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CivitCommercialUse
{
    None,
    Image,
    Rent,
    Sell
}
