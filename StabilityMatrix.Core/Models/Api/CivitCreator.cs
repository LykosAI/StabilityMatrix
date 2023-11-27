using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public record CivitCreator
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonIgnore]
    public string? ProfileUrl => Username is null ? null : $"https://civitai.com/user/{Username}";
}
