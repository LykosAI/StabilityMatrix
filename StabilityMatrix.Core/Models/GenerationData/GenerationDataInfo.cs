namespace StabilityMatrix.Core.Models.GenerationData;

/// <summary>
/// Generation data found in an image or video file.
/// </summary>
public record GenerationDataInfo
{
    public required GenerationDataSource Source { get; init; }

    /// <summary>
    /// The tool that made the file when it can be told apart from the format,
    /// e.g. "Forge Neo" or "SD.Next" for files in the AUTOMATIC1111 format.
    /// </summary>
    public string? Software { get; init; }

    public GenerationParameters? Parameters { get; init; }

    public string DisplayName => Software ?? Source.GetDisplayName();

    public bool IsProvenanceOnly => Source is GenerationDataSource.C2pa or GenerationDataSource.Iptc;
}
