using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class GithubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; }
}
