using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Converters.Json;

public class ParsableStringValueJsonConverter<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
> : JsonConverter<T>
    where T : StringValue, IParsable<T>
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
            return default;
        }

        // Use TryParse result if available
        if (T.TryParse(value, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // Otherwise use Activator
        return (T?)Activator.CreateInstance(typeToConvert, value);
    }

    /// <inheritdoc />
    public override T ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        var value = reader.GetString();
        if (value is null)
        {
            throw new JsonException("Property name cannot be null");
        }

        // Use TryParse result if available
        if (T.TryParse(value, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // Otherwise use Activator
        return (T?)Activator.CreateInstance(typeToConvert, value)
            ?? throw new JsonException("Property name cannot be null");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value is IFormattable formattable)
        {
            writer.WriteStringValue(formattable.ToString(null, CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            throw new JsonException("Property name cannot be null");
        }

        if (value is IFormattable formattable)
        {
            writer.WriteStringValue(formattable.ToString(null, CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
