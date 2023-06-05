using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum CivitModelType
{
    Checkpoint,
    TextualInversion,
    Hypernetwork,
    AestheticGradient,
    LORA,
    Controlnet,
    Poses,
    Model,
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
