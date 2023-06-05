using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitModelsResponse
{
    [JsonPropertyName("items")]
    public CivitModel[]? Items { get; set; }
    
    [JsonPropertyName("metadata")]
    public CivitMetadata? Metadata { get; set; }
}
