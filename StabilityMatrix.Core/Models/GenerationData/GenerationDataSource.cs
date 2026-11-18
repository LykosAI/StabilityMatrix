namespace StabilityMatrix.Core.Models.GenerationData;

/// <summary>
/// Format of the generation data embedded in an image or video.
/// Tools that write the format of another one (Forge writes the AUTOMATIC1111 format)
/// are reported with that format, <see cref="GenerationDataInfo.Software"/> names the actual tool.
/// </summary>
public enum GenerationDataSource
{
    StabilityMatrix,
    Automatic1111,
    ComfyUI,
    SwarmUI,
    Fooocus,
    RuinedFooocus,
    InvokeAI,
    NovelAI,
    EasyDiffusion,
    DrawThings,
    TensorArt,
    Civitai,
    HuggingFaceSpace,
    Dezgo,
    KusArt,

    /// <summary>
    /// No generation data, but C2PA manifests (Content Credentials) that label the image as AI made.
    /// </summary>
    C2pa,

    /// <summary>
    /// No generation data, but an IPTC digital source type that labels the image as AI made.
    /// </summary>
    Iptc,
}

public static class GenerationDataSourceExtensions
{
    public static string GetDisplayName(this GenerationDataSource source) =>
        source switch
        {
            GenerationDataSource.StabilityMatrix => "Stability Matrix",
            GenerationDataSource.Automatic1111 => "AUTOMATIC1111",
            GenerationDataSource.ComfyUI => "ComfyUI",
            GenerationDataSource.SwarmUI => "SwarmUI",
            GenerationDataSource.Fooocus => "Fooocus",
            GenerationDataSource.RuinedFooocus => "RuinedFooocus",
            GenerationDataSource.InvokeAI => "InvokeAI",
            GenerationDataSource.NovelAI => "NovelAI",
            GenerationDataSource.EasyDiffusion => "Easy Diffusion",
            GenerationDataSource.DrawThings => "Draw Things",
            GenerationDataSource.TensorArt => "TensorArt",
            GenerationDataSource.Civitai => "Civitai",
            GenerationDataSource.HuggingFaceSpace => "Hugging Face Space",
            GenerationDataSource.Dezgo => "Dezgo",
            GenerationDataSource.KusArt => "KusArt",
            GenerationDataSource.C2pa => "C2PA",
            GenerationDataSource.Iptc => "IPTC",
            _ => source.ToString(),
        };
}
