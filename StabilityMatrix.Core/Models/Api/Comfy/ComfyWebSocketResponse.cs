using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyWebSocketResponse
{
    [JsonPropertyName("type")]
    public required ComfyWebSocketResponseType Type { get; set; }

    /// <summary>
    /// Depending on the value of <see cref="Type"/>,
    /// this property will be one of these types
    /// <list type="bullet">
    /// <item>Status - <see cref="ComfyWebSocketStatusData"/></item>
    /// <item>Progress - <see cref="ComfyWebSocketProgressData"/></item>
    /// <item>Executing - <see cref="ComfyWebSocketExecutingData"/></item>
    /// </list>
    /// </summary>
    [JsonPropertyName("data")]
    public required JsonObject Data { get; set; }

    public T? GetDataAsType<T>(JsonSerializerOptions? options = null)
        where T : class
    {
        return Data.Deserialize<T>(options);
    }
}
