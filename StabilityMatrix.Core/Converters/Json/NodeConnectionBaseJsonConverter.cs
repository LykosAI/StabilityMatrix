using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Core.Converters.Json;

public class NodeConnectionBaseJsonConverter : JsonConverter<NodeConnectionBase>
{
    /// <inheritdoc />
    public override NodeConnectionBase Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        // Read as Data array
        reader.Read();
        var data = new object[2];
        reader.Read();
        data[0] = reader.GetString() ?? throw new JsonException("Expected string for node name");
        reader.Read();
        data[1] = reader.GetInt32();
        reader.Read();

        if (Activator.CreateInstance(typeToConvert) is not NodeConnectionBase instance)
        {
            throw new JsonException($"Failed to create instance of {typeToConvert}");
        }

        var propertyInfo =
            typeToConvert.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new JsonException($"Failed to get Data property of {typeToConvert}");

        propertyInfo.SetValue(instance, data);

        return instance;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, NodeConnectionBase value, JsonSerializerOptions options)
    {
        // Write as Data array
        writer.WriteStartArray();
        writer.WriteStringValue(value.Data?[0] as string);
        writer.WriteNumberValue((int)value.Data?[1]!);
        writer.WriteEndArray();
    }
}
