using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Models;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
public enum SharedFolderType
{
    StableDiffusion,
    Lora,
    LyCORIS,
    ESRGAN,
    GFPGAN,
    BSRGAN,
    Codeformer,
    Diffusers,
    RealESRGAN,
    SwinIR,
    VAE,
    ApproxVAE,
    Karlo,
    DeepDanbooru,
    TextualInversion,
    Hypernetwork,
    ControlNet,
    LDSR,
    CLIP,
    ScuNET,
}
