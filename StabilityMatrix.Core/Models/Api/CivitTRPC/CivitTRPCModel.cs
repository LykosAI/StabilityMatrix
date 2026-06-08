using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

/// <summary>
/// Response shape for the tRPC <c>model.getById</c> endpoint.
/// Field names follow CivitAI's internal model (some differ from the public REST API:
/// <c>user</c> instead of <c>creator</c>, <c>tagsOnModels</c> instead of <c>tags</c>, etc.).
/// Only the fields we actually consume are typed — anything else is ignored.
/// </summary>
public class CivitTRPCModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public CivitModelType Type { get; set; }

    [JsonPropertyName("nsfw")]
    public bool Nsfw { get; set; }

    [JsonPropertyName("modelVersions")]
    public List<CivitTRPCModelVersion>? ModelVersions { get; set; }
}

public class CivitTRPCModelVersion
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("baseModel")]
    public string? BaseModel { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("trainedWords")]
    public string[]? TrainedWords { get; set; }

    [JsonPropertyName("nsfwLevel")]
    public int NsfwLevel { get; set; }

    [JsonPropertyName("files")]
    public List<CivitTRPCFile>? Files { get; set; }

    /// <summary>
    /// Whether the file can actually be downloaded. False = on-site-generation only.
    /// Not exposed by the public REST API.
    /// </summary>
    [JsonPropertyName("canDownload")]
    public bool? CanDownload { get; set; }

    /// <summary>
    /// Whether on-site generation is supported for this version.
    /// Not exposed by the public REST API.
    /// </summary>
    [JsonPropertyName("canGenerate")]
    public bool? CanGenerate { get; set; }
}

public class CivitTRPCFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("sizeKB")]
    public double SizeKb { get; set; }

    [JsonPropertyName("type")]
    public CivitFileType Type { get; set; }

    [JsonPropertyName("visibility")]
    public string? Visibility { get; set; }

    [JsonPropertyName("metadata")]
    public CivitFileMetadata? Metadata { get; set; }

    [JsonPropertyName("pickleScanResult")]
    public string? PickleScanResult { get; set; }

    [JsonPropertyName("virusScanResult")]
    public string? VirusScanResult { get; set; }

    [JsonPropertyName("scannedAt")]
    public DateTime? ScannedAt { get; set; }

    [JsonPropertyName("hashes")]
    public List<CivitTRPCFileHash>? Hashes { get; set; }
}

public class CivitTRPCFileHash
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}
