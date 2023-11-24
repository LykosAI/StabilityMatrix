using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Converters.Json;

public class DefaultUnknownEnumConverter<T> : JsonConverter<T>
    where T : Enum
{
    // Get EnumMember attribute value
    private Dictionary<string, T>? _enumMemberValues;

    private IReadOnlyDictionary<string, T> EnumMemberValues =>
        _enumMemberValues ??= typeof(T)
            .GetFields()
            .Where(field => field.IsStatic)
            .Select(
                field =>
                    new
                    {
                        Field = field,
                        Attribute = field
                            .GetCustomAttributes<EnumMemberAttribute>(false)
                            .FirstOrDefault()
                    }
            )
            .Where(field => field.Attribute != null)
            .ToDictionary(
                field => field.Attribute!.Value!.ToString(),
                field => (T)field.Field.GetValue(null)!
            );

    public override T Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        var enumText = reader.GetString()?.Replace(" ", "_");
        if (Enum.TryParse(typeof(T), enumText, true, out var result))
        {
            return (T)result!;
        }

        // Try using enum member values
        if (enumText != null)
        {
            if (EnumMemberValues.TryGetValue(enumText, out var enumMemberResult))
            {
                return enumMemberResult;
            }
        }

        // Unknown value handling
        if (Enum.TryParse(typeof(T), "Unknown", true, out var unknownResult))
        {
            return (T)unknownResult!;
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

        writer.WriteStringValue(value.GetStringValue().Replace("_", " "));
    }

    /// <inheritdoc />
    public override T ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException();
        }

        var enumText = reader.GetString()?.Replace(" ", "_");
        if (Enum.TryParse(typeof(T), enumText, true, out var result))
        {
            return (T)result!;
        }

        // Unknown value handling
        if (Enum.TryParse(typeof(T), "Unknown", true, out var unknownResult))
        {
            return (T)unknownResult!;
        }

        throw new JsonException($"Unable to parse '{enumText}' to enum '{typeof(T)}'.");
    }

    /// <inheritdoc />
    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        T? value,
        JsonSerializerOptions options
    )
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WritePropertyName(value.GetStringValue().Replace("_", " "));
    }
}
