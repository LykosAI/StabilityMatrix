using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Converters.Json;

/// <summary>
/// Json converter for types that serialize to string by `ToString()` and
/// can be created by `Activator.CreateInstance(Type, string)`
/// </summary>
public class StringJsonConverter<T> : JsonConverter<T>
{
    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        var value = reader.GetString();
        if (value is null)
        {
            throw new JsonException();
        }

        return (T) Activator.CreateInstance(typeToConvert, value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.ToString());
    }
}
