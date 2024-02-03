using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter<CivitBaseModelType>))]
public enum CivitBaseModelType
{
    All,

    [StringValue("SD 1.5")]
    Sd15,

    [StringValue("SD 1.5 LCM")]
    Sd15Lcm,

    [StringValue("SD 2.1")]
    Sd21,

    [StringValue("SDXL 0.9")]
    Sdxl09,

    [StringValue("SDXL 1.0")]
    Sdxl10,

    [StringValue("SDXL 1.0 LCM")]
    Sdxl10Lcm,

    [StringValue("SDXL Turbo")]
    SdxlTurbo,
    Other,
}
