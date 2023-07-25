using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class ProgressResponse
{
    // Range from 0 to 1
    [JsonPropertyName("progress")]
    public float Progress { get; set; }
    
    // ETA in seconds
    [JsonPropertyName("eta_relative")]
    public float EtaRelative { get; set; }
    
    // state: dict
    
    // The current image in base64 format. opts.show_progress_every_n_steps is required for this to work
    [JsonPropertyName("current_image")]
    public string? CurrentImage { get; set; }
    
    [JsonPropertyName("textinfo")]
    public string? TextInfo { get; set; }
    
}
