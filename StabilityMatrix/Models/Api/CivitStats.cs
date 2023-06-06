using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitStats
{
    [JsonPropertyName("downloadCount")]
    public int DownloadCount { get; set; }
    
    [JsonPropertyName("ratingCount")]
    public int RatingCount { get; set; }
    
    [JsonPropertyName("rating")]
    public double Rating { get; set; }
}
