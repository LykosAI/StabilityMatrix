namespace StabilityMatrix.Core.Models.Database;

[Flags]
public enum LocalImageFileType : ulong
{
    // Source
    Automatic = 1 << 1,
    Comfy = 1 << 2,
    Inference = 1 << 3,

    // Generation Type
    TextToImage = 1 << 10,
    ImageToImage = 1 << 11
}
