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
}
