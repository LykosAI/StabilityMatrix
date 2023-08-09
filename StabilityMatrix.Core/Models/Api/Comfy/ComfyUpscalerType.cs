using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api.Comfy;

[JsonConverter(typeof(DefaultUnknownEnumConverter<ComfyUpscalerType>))]
public enum ComfyUpscalerType
{
    Unknown,
    Latent,
    ESRGAN
}
