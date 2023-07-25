using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class ProgressRequest
{
    [JsonPropertyName("skip_current_image")]
    public bool? SkipCurrentImage { get; set; }
}
