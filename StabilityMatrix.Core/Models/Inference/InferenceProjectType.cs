namespace StabilityMatrix.Core.Models.Inference;

/// <summary>
/// The document type of a saved Inference tab. Member names are wire format: they are
/// persisted as strings in .smproj files and used as dock-layout settings keys, so they
/// must never be renamed — display names live in <c>InferenceProjectTypeExtensions</c>
/// and may differ. In particular <see cref="WanTextToVideo"/> / <see cref="WanImageToVideo"/>
/// are the generic video tabs ("Text to Video" / "Image to Video"), which host multiple
/// model families via workflow profiles.
/// </summary>
public enum InferenceProjectType
{
    Unknown,
    TextToImage,
    ImageToImage,
    Inpainting,
    Upscale,

    /// <summary>The legacy SVD image-to-video tab.</summary>
    ImageToVideo,
    FluxTextToImage,

    /// <summary>The generic text-to-video tab (frozen pre-rename name).</summary>
    WanTextToVideo,

    /// <summary>The generic image-to-video tab (frozen pre-rename name).</summary>
    WanImageToVideo,
}
