using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtThumbnail
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("url")]
    public Uri Url { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
