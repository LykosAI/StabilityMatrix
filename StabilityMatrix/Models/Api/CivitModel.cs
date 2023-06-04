using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("type")]
    public CivitModelType Type { get; set; }
    
    [JsonPropertyName("nsfw")]
    public bool Nsfw { get; set; }
    
    [JsonPropertyName("tags")]
    public string[] Tags { get; set; }
    
    [JsonPropertyName("mode")]
    public CivitMode? Mode { get; set; }
    
    [JsonPropertyName("creator")]
    public CivitCreator Creator { get; set; }
    
    [JsonPropertyName("stats")]
    public CivitModelStats Stats { get; set; }

    [JsonPropertyName("modelVersions")]
    public CivitModelVersion[] ModelVersions { get; set; }
}
