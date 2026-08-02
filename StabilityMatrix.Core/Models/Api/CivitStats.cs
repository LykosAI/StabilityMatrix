using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api;

public record CivitStats
{
    [JsonPropertyName("downloadCount")]
    [JsonConverter(typeof(NullToDefaultJsonConverter<int>))]
    public int DownloadCount { get; set; }

    [JsonPropertyName("ratingCount")]
    [JsonConverter(typeof(NullToDefaultJsonConverter<int>))]
    public int RatingCount { get; set; }

    [JsonPropertyName("rating")]
    [JsonConverter(typeof(NullToDefaultJsonConverter<double>))]
    public double Rating { get; set; }
}
