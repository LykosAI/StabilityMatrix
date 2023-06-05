using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Extensions;

namespace StabilityMatrix.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum CivitModelType
{
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
    LoCon
}

public static class CivitModelTypeExtensions
{
    public static SharedFolderType ToSharedFolderType(this CivitModelType type)
    {
        return type switch
        {
            CivitModelType.Checkpoint => SharedFolderType.StableDiffusion,
            CivitModelType.TextualInversion => SharedFolderType.TextualInversion,
            CivitModelType.Hypernetwork => SharedFolderType.Hypernetwork,
            //CivitModelType.AestheticGradient => expr,
            CivitModelType.LORA => SharedFolderType.Lora,
            CivitModelType.Controlnet => SharedFolderType.ControlNet,
            //CivitModelType.Poses => expr,
            CivitModelType.Model => SharedFolderType.StableDiffusion,
            CivitModelType.LoCon => SharedFolderType.LyCORIS,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
