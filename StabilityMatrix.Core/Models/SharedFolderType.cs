using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[Flags]
public enum SharedFolderType
{
    Unknown = 0,

    [Description("Base Models")]
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

    [Description("TextualInversion (Embeddings)")]
    TextualInversion = 1 << 14,
    Hypernetwork = 1 << 15,
    ControlNet = 1 << 16,
    LDSR = 1 << 17,
    CLIP = 1 << 18,
    ScuNET = 1 << 19,
    GLIGEN = 1 << 20,
    AfterDetailer = 1 << 21,
    IpAdapter = 1 << 22,
    T2IAdapter = 1 << 23,

    InvokeIpAdapters15 = 1 << 24,
    InvokeIpAdaptersXl = 1 << 25,
    InvokeClipVision = 1 << 26,
    SVD = 1 << 27,

    PromptExpansion = 1 << 30,
    Unet = 1 << 31
}
