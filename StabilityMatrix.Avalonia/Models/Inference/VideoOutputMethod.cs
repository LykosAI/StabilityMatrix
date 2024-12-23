using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonConverter(typeof(JsonStringEnumConverter<VideoOutputMethod>))]
public enum VideoOutputMethod
{
    Fastest,
    Default,
    Slowest,
}
