using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public enum ComfyUpscalerType
{
    None,
    Latent,
    ESRGAN,
    DownloadableModel
}
