using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class CivitEnumsResponse
{
    [JsonPropertyName("ActiveBaseModel")]
    public List<string>? ActiveBaseModel { get; init; }

    [JsonPropertyName("BaseModel")]
    public List<string>? BaseModel { get; init; }
}
