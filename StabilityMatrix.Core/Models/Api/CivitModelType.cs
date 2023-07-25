using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(DefaultUnknownEnumConverter<CivitModelType>))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum CivitModelType
{
    Unknown,
    [ConvertTo<SharedFolderType>(SharedFolderType.StableDiffusion)]
    Checkpoint,
    [ConvertTo<SharedFolderType>(SharedFolderType.TextualInversion)]
    TextualInversion,
    [ConvertTo<SharedFolderType>(SharedFolderType.Hypernetwork)]
    Hypernetwork,
    AestheticGradient,
    [ConvertTo<SharedFolderType>(SharedFolderType.Lora)]
    LORA,
    [ConvertTo<SharedFolderType>(SharedFolderType.ControlNet)]
    Controlnet,
    Poses,
    [ConvertTo<SharedFolderType>(SharedFolderType.StableDiffusion)]
    Model,
    [ConvertTo<SharedFolderType>(SharedFolderType.LyCORIS)]
    LoCon,
    All,
}
