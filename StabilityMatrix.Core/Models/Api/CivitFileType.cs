using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(DefaultUnknownEnumConverter<CivitFileType>))]
public enum CivitFileType
{
    Unknown,
    Model,
    VAE,
    Config,
    Archive,

    [EnumMember(Value = "Pruned Model")]
    PrunedModel,

    [EnumMember(Value = "Training Data")]
    TrainingData,

    [EnumMember(Value = "Diffusion Model")]
    DiffusionModel,

    [EnumMember(Value = "Text Encoder")]
    TextEncoder,

    [EnumMember(Value = "Vision Encoder")]
    VisionEncoder,

    Negative,
    UNet,
    CLIPVision,
    ControlNet,
    Workflow,
    Upscaler,

    [EnumMember(Value = "Enhancement LoRA")]
    EnhancementLora,

    Other,
}

public static class CivitFileTypeExtensions
{
    /// <summary>
    /// True for file types that carry the primary model weights: full/pruned checkpoints and
    /// UNet-only diffusion models. Used for install detection and default file selection.
    /// </summary>
    public static bool IsModelWeights(this CivitFileType type) =>
        type
            is CivitFileType.Model
                or CivitFileType.PrunedModel
                or CivitFileType.DiffusionModel
                or CivitFileType.UNet;

    /// <summary>
    /// True for file types worth listing as downloadable files in the model browser —
    /// model weights plus companion components (VAE, text/vision encoders, etc).
    /// </summary>
    public static bool IsDownloadableModelFile(this CivitFileType type) =>
        type.IsModelWeights()
        || type
            is CivitFileType.VAE
                or CivitFileType.TextEncoder
                or CivitFileType.VisionEncoder
                or CivitFileType.CLIPVision
                or CivitFileType.ControlNet
                or CivitFileType.Upscaler
                or CivitFileType.Negative
                or CivitFileType.EnhancementLora;

    /// <summary>
    /// Maps file types that unambiguously determine their destination shared folder,
    /// regardless of the parent model's type. Returns null when the destination
    /// depends on the model type instead (e.g. plain "Model" files).
    /// </summary>
    public static SharedFolderType? GetExplicitSharedFolderType(this CivitFileType type) =>
        type switch
        {
            CivitFileType.VAE => SharedFolderType.VAE,
            CivitFileType.DiffusionModel or CivitFileType.UNet => SharedFolderType.DiffusionModels,
            CivitFileType.TextEncoder => SharedFolderType.TextEncoders,
            CivitFileType.VisionEncoder or CivitFileType.CLIPVision => SharedFolderType.ClipVision,
            CivitFileType.ControlNet => SharedFolderType.ControlNet,
            CivitFileType.Upscaler => SharedFolderType.ESRGAN,
            _ => null,
        };

    /// <summary>
    /// Resolves the destination shared folder for a file: explicit file types win; the legacy
    /// fallback then guesses UNet-only checkpoints for files still typed plain "Model"
    /// (base model name prefixes and GGUF format); otherwise the model type decides.
    /// </summary>
    public static SharedFolderType GetSharedFolderType(
        this CivitFileType type,
        CivitModelType modelType,
        string? baseModelType,
        string? fileName = null,
        CivitModelFormat? format = null
    )
    {
        if (type.GetExplicitSharedFolderType() is { } explicitFolder)
        {
            return explicitFolder;
        }

        if (modelType is CivitModelType.Checkpoint)
        {
            var isUnetOnly =
                baseModelType?.StartsWith("Wan", StringComparison.OrdinalIgnoreCase) is true
                || baseModelType?.StartsWith("Flux", StringComparison.OrdinalIgnoreCase) is true
                || baseModelType?.StartsWith("Hunyuan", StringComparison.OrdinalIgnoreCase) is true
                || format == CivitModelFormat.GGUF
                || (
                    fileName is not null
                    && Path.GetExtension(fileName).Equals(".gguf", StringComparison.OrdinalIgnoreCase)
                );

            if (isUnetOnly)
            {
                return SharedFolderType.DiffusionModels;
            }
        }

        return modelType.ConvertTo<SharedFolderType>();
    }
}
