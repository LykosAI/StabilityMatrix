using System;
using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Models;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[Flags]
public enum SharedFolderType
{
    StableDiffusion = 1 << 0,
    Lora = 1 << 1,
    LyCORIS = 1 << 2,
    ESRGAN = 1 << 3,
    GFPGAN = 1 << 4,
    BSRGAN = 1 << 5,
    Codeformer = 1 << 6,
    Diffusers = 1 << 7,
    RealESRGAN = 1 << 8,
    SwinIR = 1 << 9,
    VAE = 1 << 10,
    ApproxVAE = 1 << 11,
    Karlo = 1 << 12,
    DeepDanbooru = 1 << 13,
    TextualInversion = 1 << 14,
    Hypernetwork = 1 << 15,
    ControlNet = 1 << 16,
    LDSR = 1 << 17,
    CLIP = 1 << 18,
    ScuNET = 1 << 19,
    GLIGEN = 1 << 20,
}
