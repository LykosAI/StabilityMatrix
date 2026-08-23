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

    [ConvertTo<SharedFolderType>(SharedFolderType.Embeddings)]
    TextualInversion,

    [ConvertTo<SharedFolderType>(SharedFolderType.Hypernetwork)]
    Hypernetwork,

    [ConvertTo<SharedFolderType>(SharedFolderType.Lora)]
    DoRA,

    [ConvertTo<SharedFolderType>(SharedFolderType.Lora)]
    LORA,

    [ConvertTo<SharedFolderType>(SharedFolderType.ControlNet)]
    Controlnet,

    [ConvertTo<SharedFolderType>(SharedFolderType.LyCORIS)]
    LoCon,

    [ConvertTo<SharedFolderType>(SharedFolderType.VAE)]
    VAE,

    // Unused/obsolete/unknown/meta options
    AestheticGradient,
    Model,
    MotionModule,
    Poses,

    [ConvertTo<SharedFolderType>(SharedFolderType.ESRGAN)]
    Upscaler,

    Wildcards,
    Workflows,
    Other,
    All,
}

public static class CivitModelTypeExtensions
{
    /// <summary>
    /// True for model types the model browser can list and import: types with a shared-folder
    /// destination, plus <see cref="CivitModelType.Workflows"/>, which imports into the
    /// workflow library instead of the models directory.
    /// </summary>
    public static bool IsBrowsable(this CivitModelType type) =>
        type is CivitModelType.Workflows || type.ConvertTo<SharedFolderType>() > 0;
}
