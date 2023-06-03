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
    RealESRGAN,
    SwinIR,
    VAE,
    ApproxVAE,
}
