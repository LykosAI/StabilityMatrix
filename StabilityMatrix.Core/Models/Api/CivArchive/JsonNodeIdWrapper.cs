using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivArchive;

[JsonConverter(typeof(JsonNodeIdWrapperConverter))]
public class JsonNodeIdWrapper(string value)
{
    public string Value { get; } = value;

    public override string ToString() => Value;
}

public sealed class JsonNodeIdWrapperConverter : JsonConverter<JsonNodeIdWrapper>
{
    public override JsonNodeIdWrapper Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => new JsonNodeIdWrapper(reader.GetString() ?? string.Empty),
            JsonTokenType.Number => new JsonNodeIdWrapper(reader.GetInt64().ToString()),
            _ => throw new JsonException($"Unsupported id token {reader.TokenType}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, JsonNodeIdWrapper value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
