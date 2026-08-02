using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api;

public record CivitModelStats : CivitStats
{
    [JsonPropertyName("favoriteCount")]
    [JsonConverter(typeof(NullToDefaultJsonConverter<int>))]
    public int FavoriteCount { get; set; }

    [JsonPropertyName("commentCount")]
    [JsonConverter(typeof(NullToDefaultJsonConverter<int>))]
    public int CommentCount { get; set; }

    [JsonPropertyName("thumbsUpCount")]
    [JsonConverter(typeof(NullToDefaultJsonConverter<int>))]
    public int ThumbsUpCount { get; set; }
}
