using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyHistoryStatus
{
    [JsonPropertyName("status_str")]
    public string? StatusStr { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    /// <summary>
    /// Execution events recorded for the prompt, each as a [eventName, data] pair
    /// </summary>
    [JsonPropertyName("messages")]
    public List<JsonElement>? Messages { get; set; }
}
