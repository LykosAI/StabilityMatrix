using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyInputInfo
{
    [JsonPropertyName("required")]
    public Dictionary<string, JsonValue>? Required { get; set; }

    public List<string>? GetRequiredValueAsNestedList(string key)
    {
        var value = Required?[key];
        if (value is null) return null;

        var nested = value.Deserialize<List<List<string>>>();
        
        return nested?.SelectMany(x => x).ToList();
    }
}
