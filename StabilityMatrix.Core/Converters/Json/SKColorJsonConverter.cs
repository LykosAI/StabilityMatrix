using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace StabilityMatrix.Core.Converters.Json;

public class SKColorJsonConverter : JsonConverter<SKColor>
{
    public override SKColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!reader.TryGetUInt32(out var value))
        {
            return SKColor.Empty;
        }

        return new SKColor(value);
    }

    public override void Write(Utf8JsonWriter writer, SKColor value, JsonSerializerOptions options)
    {
        // Convert to uint in the format ARGB
        var argbColor = (uint)((value.Alpha << 24) | (value.Red << 16) | (value.Green << 8) | value.Blue);

        writer.WriteNumberValue(argbColor);
    }
}
