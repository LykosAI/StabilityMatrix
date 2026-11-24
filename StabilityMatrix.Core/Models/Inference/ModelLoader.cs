using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Inference;

public enum ModelLoader
{
    [StringValue("Default")]
    Default,

    [StringValue("GGUF")]
    Gguf,

    [StringValue("nf4")]
    Nf4,

    [StringValue("UNet")]
    Unet,
}
