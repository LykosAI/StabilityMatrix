using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class CivitModelVersion
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }

    [JsonPropertyName("trainedWords")]
    public string[] TrainedWords { get; set; }

    [JsonPropertyName("baseModel")]
    public string? BaseModel { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("files")]
    public List<CivitFile>? Files { get; set; }

    [JsonPropertyName("images")]
    public List<CivitImage>? Images { get; set; }

    [JsonPropertyName("stats")]
    public CivitModelStats Stats { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("earlyAccessDeadline")]
    public DateTimeOffset? EarlyAccessDeadline { get; set; }

    /// <summary>
    /// Whether the version is currently in early access. CivitAI signals this on current
    /// API responses via a future <see cref="EarlyAccessDeadline"/> while reporting
    /// <see cref="Availability"/> as "Public"; the availability check is kept for
    /// responses that do use the explicit value.
    /// </summary>
    [JsonIgnore]
    public bool IsEarlyAccess =>
        (Availability?.Equals("EarlyAccess", StringComparison.OrdinalIgnoreCase) ?? false)
        || EarlyAccessDeadline > DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the alternative main model files for this release. Component file types count
    /// only when they are the artifact published by the parent model type.
    /// </summary>
    public IEnumerable<CivitFile> GetMainModelFiles(CivitModelType modelType) =>
        (Files ?? []).Where(file =>
            file is not null
            && (
                file.Type.IsModelWeights()
                || modelType switch
                {
                    CivitModelType.VAE => file.Type is CivitFileType.VAE,
                    CivitModelType.Upscaler => file.Type is CivitFileType.Upscaler,
                    CivitModelType.Controlnet => file.Type is CivitFileType.ControlNet,
                    CivitModelType.TextualInversion => file.Type is CivitFileType.Negative,
                    CivitModelType.LORA or CivitModelType.LoCon or CivitModelType.DoRA => file.Type
                        is CivitFileType.EnhancementLora,
                    _ => false,
                }
            )
        );

    /// <summary>
    /// Whether at least one main model variant is present in the local hash index.
    /// This does not indicate whether external dependencies needed to run it are installed.
    /// </summary>
    public bool IsInstalled(CivitModelType modelType, IReadOnlySet<string> installedHashes) =>
        GetMainModelFiles(modelType)
            .Any(file =>
                file.Hashes?.BLAKE3 is { } hash
                && !string.IsNullOrWhiteSpace(hash)
                && installedHashes.Contains(hash)
            );
}
