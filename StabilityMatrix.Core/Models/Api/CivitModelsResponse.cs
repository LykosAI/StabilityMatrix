using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class CivitModelsResponse
{
    [JsonPropertyName("items")]
    public List<CivitModel>? Items { get; set; }
    
    [JsonPropertyName("metadata")]
    public CivitMetadata? Metadata { get; set; }
}
