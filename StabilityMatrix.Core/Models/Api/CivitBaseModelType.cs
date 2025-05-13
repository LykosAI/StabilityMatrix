using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter<CivitBaseModelType>))]
public enum CivitBaseModelType
{
    All,

    [StringValue("AuraFlow")]
    AuraFlow,

    [StringValue("CogVideoX")]
    CogVideoX,

    [StringValue("Flux.1 S")]
    Flux1S,

    [StringValue("Flux.1 D")]
    Flux1D,

    [StringValue("HiDream")]
    HiDream,

    [StringValue("Hunyuan 1")]
    Hunyuan1,

    [StringValue("Hunyuan Video")]
    HunyuanVideo,

    [StringValue("Illustrious")]
    Illustrious,

    [StringValue("Kolors")]
    Kolors,

    [StringValue("LTXV")]
    Ltxv,

    [StringValue("Lumina")]
    Lumina,

    [StringValue("Mochi")]
    Mochi,

    [StringValue("NoobAI")]
    NoobAi,

    [StringValue("ODOR")]
    Odor,

    [StringValue("PixArt a")]
    PixArtA,

    [StringValue("PixArt E")]
    PixArtE,

    [StringValue("Playground v2")]
    PlaygroundV2,

    [StringValue("Pony")]
    Pony,

    [StringValue("SD 1.4")]
    Sd14,

    [StringValue("SD 1.5")]
    Sd15,

    [StringValue("SD 1.5 Hyper")]
    Sd15Hyper,

    [StringValue("SD 1.5 LCM")]
    Sd15Lcm,

    [StringValue("SD 2.0")]
    Sd20,

    [StringValue("SD 2.0 768")]
    Sd20_768,

    [StringValue("SD 2.1")]
    Sd21,

    [StringValue("SD 2.1 768")]
    Sd21_768,

    [StringValue("SD 2.1 Unclip")]
    Sd21Unclip,

    [StringValue("SD 3")]
    Sd3,

    [StringValue("SD 3.5")]
    Sd35,

    [StringValue("SD 3.5 Large")]
    Sd35Large,

    [StringValue("SD 3.5 Large Turbo")]
    Sd35LargeTurbo,

    [StringValue("SD 3.5 Medium")]
    Sd35Medium,

    [StringValue("SDXL 0.9")]
    Sdxl09,

    [StringValue("SDXL 1.0")]
    Sdxl10,

    [StringValue("SDXL 1.0 LCM")]
    Sdxl10Lcm,

    [StringValue("SDXL Distilled")]
    SdxlDistilled,

    [StringValue("SDXL Hyper")]
    SdxlHyper,

    [StringValue("SDXL Lightning")]
    SdxlLightning,

    [StringValue("SDXL Turbo")]
    SdxlTurbo,

    [StringValue("SVD")]
    SVD,

    [StringValue("SVD XT")]
    SvdXt,

    [StringValue("Stable Cascade")]
    StableCascade,

    [StringValue("Wan Video")]
    WanVideo,

    Other,
}
