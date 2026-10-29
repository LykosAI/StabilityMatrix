using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyHistoryResponse
{
    [JsonPropertyName("outputs")]
    public required Dictionary<string, ComfyHistoryOutput> Outputs { get; set; }
}
