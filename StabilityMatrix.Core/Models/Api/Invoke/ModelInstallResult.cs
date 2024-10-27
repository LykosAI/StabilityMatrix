using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Invoke;

public class ModelInstallResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error_reason")]
    public string? ErrorReason { get; set; }

    [JsonPropertyName("inplace")]
    public bool Inplace { get; set; }

    [JsonPropertyName("local_path")]
    public string? LocalPath { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_traceback")]
    public string? ErrorTraceback { get; set; }
}
