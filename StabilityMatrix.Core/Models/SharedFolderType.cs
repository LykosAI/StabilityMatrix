using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Core.Models;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[Flags]
public enum SharedFolderType : ulong
{
    Unknown = 0,

    [Extensions.Description("Checkpoints")]
    StableDiffusion = 1 << 0,
    Lora = 1 << 1,
    LyCORIS = 1 << 2,

    [Extensions.Description("Upscalers (ESRGAN)")]
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
    Embeddings = 1 << 14,
    Hypernetwork = 1 << 15,
    ControlNet = 1 << 16,
    LDSR = 1 << 17,
    TextEncoders = 1 << 18,
    ScuNET = 1 << 19,
    GLIGEN = 1 << 20,
    AfterDetailer = 1 << 21,
    IpAdapter = 1 << 22,
    T2IAdapter = 1 << 23,
    IpAdapters15 = 1 << 24,
    IpAdaptersXl = 1 << 25,
    ClipVision = 1 << 26,
    SVD = 1 << 27,
    Ultralytics = 1 << 28,
    Sams = 1 << 29,
    PromptExpansion = 1 << 30,

    [Extensions.Description("Diffusion Models (UNet-only)")]
    DiffusionModels = 1ul << 31,
}
