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

    [JsonIgnore]
    public bool IsEarlyAccess =>
        Availability?.Equals("EarlyAccess", StringComparison.OrdinalIgnoreCase) ?? false;
}
