using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace StabilityMatrix.Core.Converters.Json;

/// <summary>
/// JSON converter for SKRect - serializes as an object with Left, Top, Right, Bottom properties.
/// </summary>
public class SKRectJsonConverter : JsonConverter<SKRect>
{
    public override SKRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return SKRect.Empty;
        }

        float left = 0,
            top = 0,
            right = 0,
            bottom = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToLowerInvariant())
                {
                    case "left":
                        left = reader.GetSingle();
                        break;
                    case "top":
                        top = reader.GetSingle();
                        break;
                    case "right":
                        right = reader.GetSingle();
                        break;
                    case "bottom":
                        bottom = reader.GetSingle();
                        break;
                }
            }
        }

        return new SKRect(left, top, right, bottom);
    }

    public override void Write(Utf8JsonWriter writer, SKRect value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("left", value.Left);
        writer.WriteNumber("top", value.Top);
        writer.WriteNumber("right", value.Right);
        writer.WriteNumber("bottom", value.Bottom);
        writer.WriteEndObject();
    }
}
