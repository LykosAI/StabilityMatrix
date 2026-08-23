using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonConverter(typeof(JsonStringEnumConverter<VideoOutputFormat>))]
public enum VideoOutputFormat
{
    /// <summary>Animated WebP via <c>SaveAnimatedWEBP</c> (the original default).</summary>
    WebP,

    /// <summary>MP4 (h264) via the core <c>CreateVideo</c> + <c>SaveVideo</c> nodes.</summary>
    Mp4,
}
