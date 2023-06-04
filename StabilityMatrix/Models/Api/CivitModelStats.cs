using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitModelStats : CivitStats
{
    [JsonPropertyName("favoriteCount")]
    public int FavoriteCount { get; set; }
    
    [JsonPropertyName("commentCount")]
    public int CommentCount { get; set; }
}
