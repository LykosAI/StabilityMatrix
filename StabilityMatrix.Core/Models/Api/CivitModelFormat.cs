using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(DefaultUnknownEnumConverter<CivitModelFormat>))]
public enum CivitModelFormat
{
    Unknown,
    SafeTensor,
    PickleTensor,
    Diffusers,
    GGUF,
    Other
}
