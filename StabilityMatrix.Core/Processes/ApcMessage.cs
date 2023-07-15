using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Processes;

public readonly struct ApcMessage
{
    [JsonPropertyName("type")]
    public required ApcType Type { get; init; }
    
    [JsonPropertyName("data")]
    public required string Data { get; init; }
}
