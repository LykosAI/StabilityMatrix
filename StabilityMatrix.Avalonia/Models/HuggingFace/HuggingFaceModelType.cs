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

    [Description("ControlNets")]
    [ConvertTo<SharedFolderType>(SharedFolderType.ControlNet)]
    ControlNet,

    [Description("ControlNets (Diffusers)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.ControlNet)]
    DiffusersControlNet,

    [Description("IP Adapters")]
    [ConvertTo<SharedFolderType>(SharedFolderType.IpAdapter)]
    IpAdapter,

    [Description("IP Adapters (Diffusers)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.IpAdapter)]
    DiffusersIpAdapter,

    [Description("T2I Adapters")]
    [ConvertTo<SharedFolderType>(SharedFolderType.T2IAdapter)]
    T2IAdapter,

    [Description("T2I Adapters (Diffusers)")]
    [ConvertTo<SharedFolderType>(SharedFolderType.T2IAdapter)]
    DiffusersT2IAdapter,
}
