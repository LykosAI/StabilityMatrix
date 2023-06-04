using System;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitModelVersion
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }
    
    [JsonPropertyName("trainedWords")]
    public string[] TrainedWords { get; set; }
    
    [JsonPropertyName("files")]
    public CivitFile[] Files { get; set; }
    
    [JsonPropertyName("images")]
    public CivitImage[] Images { get; set; }
    
    [JsonPropertyName("stats")]
    public CivitModelStats Stats { get; set; }
}
