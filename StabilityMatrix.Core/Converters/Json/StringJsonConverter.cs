using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Converters.Json;

/// <summary>
/// Json converter for types that serialize to string by `ToString()` and
/// can be created by `Activator.CreateInstance(Type, string)`
/// Types implementing <see cref="IFormattable"/> will be formatted with <see cref="CultureInfo.InvariantCulture"/>
/// </summary>
[PublicAPI]
public class StringJsonConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>
    : JsonConverter<T>
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

        return (T?)Activator.CreateInstance(typeToConvert, value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
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
