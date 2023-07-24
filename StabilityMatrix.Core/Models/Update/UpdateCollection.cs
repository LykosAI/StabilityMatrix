using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Update;

public record UpdateCollection (
    [property: JsonPropertyName("win-x64")]
    UpdateInfo? WindowsX64,
    
    [property: JsonPropertyName("linux-x64")]
    UpdateInfo? LinuxX64
);
