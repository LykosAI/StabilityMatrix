using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtStats
{
    [JsonPropertyName("num_shares")]
    public int NumShares { get; set; }

    [JsonPropertyName("num_bookmarks")]
    public int NumBookmarks { get; set; }

    [JsonPropertyName("num_reviews")]
    public int NumReviews { get; set; }

    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("num_comments")]
    public int NumComments { get; set; }

    [JsonPropertyName("num_likes")]
    public int NumLikes { get; set; }

    [JsonPropertyName("num_downloads")]
    public int NumDownloads { get; set; }

    [JsonPropertyName("num_runs")]
    public int NumRuns { get; set; }

    [JsonPropertyName("num_views")]
    public int NumViews { get; set; }
}
