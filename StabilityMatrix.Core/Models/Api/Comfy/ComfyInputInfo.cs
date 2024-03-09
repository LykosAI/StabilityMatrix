using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyInputInfo
{
    [JsonPropertyName("required")]
    public Dictionary<string, JsonValue>? Required { get; set; }

    [JsonPropertyName("optional")]
    public Dictionary<string, JsonValue>? Optional { get; set; }

    public List<string>? GetRequiredValueAsNestedList(string key)
    {
        var value = Required?[key];

        var nested = value?.Deserialize<List<List<string>>>();

        return nested?.SelectMany(x => x).ToList();
    }

    public List<string>? GetOptionalValueAsNestedList(string key)
    {
        var value = Optional?[key];

        var nested = value?.Deserialize<JsonArray>()?[0].Deserialize<List<string>>();

        return nested;
    }
}
