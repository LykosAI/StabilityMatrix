using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtCreator
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("bio")]
    public string Bio { get; set; }

    [JsonPropertyName("avatar")]
    public Uri Avatar { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("dev_profile_url")]
    public string DevProfileUrl { get; set; }
}
