using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Converters.Json;

public class LaunchOptionValueJsonConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            var boolValue = reader.GetBoolean();
            return boolValue;
        }
        catch (InvalidOperationException)
        {
            // ignored
        }
        
        try
        {
            var intValue = reader.GetInt32();
            return intValue;
        }
        catch (InvalidOperationException)
        {
            // ignored
        }
        
        try
        {
            var strValue = reader.GetString();
            return strValue;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case string strValue:
                writer.WriteStringValue(strValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
