using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Persists <see cref="InferenceWorkflowProfile"/> as its stable <c>Key</c> ("Flux2") while
/// still reading the numeric values that pre-2.17 saved projects carry. Unknown keys or
/// out-of-range numbers fall back to Auto — a project saved by a newer app version with a
/// profile this build doesn't know must load rather than throw.
/// </summary>
public class InferenceWorkflowProfileJsonConverter : JsonConverter<InferenceWorkflowProfile>
{
    public override InferenceWorkflowProfile Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => InferenceWorkflowProfiles.FromLegacyValue(reader.GetInt32()),
            JsonTokenType.String => InferenceWorkflowProfiles.FromKey(reader.GetString()),
            _ => InferenceWorkflowProfile.Auto,
        };

    public override void Write(
        Utf8JsonWriter writer,
        InferenceWorkflowProfile value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStringValue(value.Key);
    }
}
