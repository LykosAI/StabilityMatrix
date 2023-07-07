using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Converters.Json;

public class DefaultUnknownEnumConverter<T> : JsonConverter<T> where T : Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) 
        {
            throw new JsonException();
        }

        var enumText = reader.GetString();
        if (Enum.TryParse(typeof(T), enumText, true, out var result))
        {
            return (T) result!;
        }

        // Unknown value handling
        if (Enum.TryParse(typeof(T), "Unknown", true, out var unknownResult)) 
        {
            return (T) unknownResult!;
        }
        
        throw new JsonException($"Unable to parse '{enumText}' to enum '{typeof(T)}'.");
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(value.GetStringValue());
    }
}
