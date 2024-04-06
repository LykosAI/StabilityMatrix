using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class CivitModelStats : CivitStats
{
    [JsonPropertyName("favoriteCount")]
    public int FavoriteCount { get; set; }

    [JsonPropertyName("commentCount")]
    public int CommentCount { get; set; }

    [JsonPropertyName("thumbsUpCount")]
    public int ThumbsUpCount { get; set; }
}
