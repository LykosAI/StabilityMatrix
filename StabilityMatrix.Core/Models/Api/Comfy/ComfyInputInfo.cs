using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyInputInfo
{
    [JsonPropertyName("required")]
    public Dictionary<string, JsonNode>? Required { get; set; }

    [JsonPropertyName("optional")]
    public Dictionary<string, JsonNode>? Optional { get; set; }

    public List<string>? GetRequiredValueAsNestedList(string key)
    {
        var value = Required?[key];

        // value usually is a [["a", "b"]] array
        // but can also be [["a", "b"], {"x": "y"}] array

        var outerArray = value?.Deserialize<JsonArray>();

        if (outerArray?.FirstOrDefault() is not { } innerNode)
        {
            return null;
        }

        var innerList = innerNode.Deserialize<List<string>>();
        return innerList;
    }

    public List<string>? GetOptionalValueAsNestedList(string key)
    {
        var value = Optional?[key];

        // value usually is a [["a", "b"]] array
        // but can also be [["a", "b"], {"x": "y"}] array

        var outerArray = value?.Deserialize<JsonArray>();

        if (outerArray?.FirstOrDefault() is not { } innerNode)
        {
            return null;
        }

        var innerList = innerNode.Deserialize<List<string>>();
        return innerList;
    }
}
