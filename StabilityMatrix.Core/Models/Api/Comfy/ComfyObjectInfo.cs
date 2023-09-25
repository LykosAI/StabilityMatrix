using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyObjectInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("category")]
    public string? Category { get; set; }
    
    [JsonPropertyName("output_node")]
    public bool IsOutputNode { get; set; }
    
    /// <summary>
    /// Input info
    /// </summary>
    [JsonPropertyName("input")]
    public required ComfyInputInfo Input { get; set; }
    
    /// <summary>
    /// List of output point types
    /// i.e. ["MODEL", "CLIP", "VAE"]
    /// </summary>
    [JsonPropertyName("output")]
    public required List<string> Output { get; set; }
    
    /// <summary>
    /// List of output point display names
    /// i.e. ["MODEL", "CLIP", "VAE"]
    /// </summary>
    [JsonPropertyName("output_name")]
    public required List<string> OutputName { get; set; }
    
    /// <summary>
    /// List of whether the indexed output is a list
    /// i.e. [false, false, false]
    /// </summary>
    [JsonPropertyName("output_is_list")]
    public required List<bool> OutputIsList { get; set; }
}
