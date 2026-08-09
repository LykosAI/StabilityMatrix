using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Converters.Json;

/// <summary>
/// Reads JSON <c>null</c> as <c>default(T)</c> for non-nullable value-type properties.
/// For APIs that send <c>null</c> where a number is expected (e.g. CivitAI stats counts),
/// where the default serializer would throw and fail the whole response.
/// Apply per-property via <see cref="JsonConverterAttribute"/> — not intended for
/// <see cref="JsonSerializerOptions.Converters"/>, since registering it globally would
/// silently accept null for every property of type <typeparamref name="T"/>.
/// </summary>
public class NullToDefaultJsonConverter<T> : JsonConverter<T>
    where T : struct
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
