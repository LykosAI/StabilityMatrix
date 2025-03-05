using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.HuggingFace;

[JsonConverter(typeof(DefaultUnknownEnumConverter<HuggingFaceModelType>))]
public enum HuggingFaceModelType
{
    [Description("Base Models")]
    [ConvertTo<SharedFolderType>(SharedFolderType.StableDiffusion)]
    BaseModel,

    [Description("CLIP / Text Encoders")]
    [ConvertTo<SharedFolderType>(SharedFolderType.CLIP)]
    Clip,

    [Description("ControlNets (SD1.5)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.ControlNet)]
    ControlNet,

    [Description("ControlNets (Diffusers SD1.5)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.ControlNet)]
    DiffusersControlNet,

    [Description("ControlNets (SDXL)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.ControlNet)]
    ControlNetXl,

    [Description("IP Adapters")]
    [ConvertTo<SharedFolderType>(SharedFolderType.IpAdapter)]
    IpAdapter,

    [Description("IP Adapters (Diffusers SD1.5)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.InvokeIpAdapters15)]
    DiffusersIpAdapter,

    [Description("IP Adapters (Diffusers SDXL)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.InvokeIpAdaptersXl)]
    DiffusersIpAdapterXl,

    [Description("CLIP Vision")]
    [ConvertTo<SharedFolderType>(SharedFolderType.InvokeClipVision)]
    DiffusersClipVision,

    [Description("T2I Adapters")]
    [ConvertTo<SharedFolderType>(SharedFolderType.T2IAdapter)]
    T2IAdapter,

    [Description("T2I Adapters (Diffusers)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.T2IAdapter)]
    DiffusersT2IAdapter,

    [Description("Ultralytics/Segmentation Models")]
    [ConvertTo<SharedFolderType>(SharedFolderType.Ultralytics)]
    Ultralytics,

    [Description("SAM Models")]
    [ConvertTo<SharedFolderType>(SharedFolderType.Sams)]
    Sams,

    [Description("UNet-Only Models")]
    [ConvertTo<SharedFolderType>(SharedFolderType.Unet)]
    Unet,

    [Description("VAE")]
    [ConvertTo<SharedFolderType>(SharedFolderType.VAE)]
    Vae,
}
