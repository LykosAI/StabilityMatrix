using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Persists <see cref="VideoWorkflowProfile"/> as its stable <c>Key</c> ("Wan"). Unknown keys
/// fall back to Auto — a project saved by a newer app version with a profile this build
/// doesn't know must load rather than throw.
/// </summary>
public class VideoWorkflowProfileJsonConverter : JsonConverter<VideoWorkflowProfile>
{
    public override VideoWorkflowProfile Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        reader.TokenType switch
        {
            JsonTokenType.String => VideoWorkflowProfile.FromKey(reader.GetString()),
            _ => VideoWorkflowProfile.Auto,
        };

    public override void Write(
        Utf8JsonWriter writer,
        VideoWorkflowProfile value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStringValue(value.Key);
    }
}
