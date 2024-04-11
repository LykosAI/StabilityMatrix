using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtDownloadResponse
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; }
}
