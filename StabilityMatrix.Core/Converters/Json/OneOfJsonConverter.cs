using System.Text.Json;
using System.Text.Json.Serialization;
using OneOf;

namespace StabilityMatrix.Core.Converters.Json;

public class OneOfJsonConverter<T1, T2> : JsonConverter<OneOf<T1, T2>>
{
    /// <inheritdoc />
    public override OneOf<T1, T2> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        // Not sure how else to do this without polymorphic type markers but that would not serialize into T1/T2
        // So just try to deserialize T1, if it fails, try T2
        Exception? t1Exception = null;
        Exception? t2Exception = null;

        try
        {
            if (JsonSerializer.Deserialize<T1>(ref reader, options) is { } t1)
            {
                return t1;
            }
        }
        catch (JsonException e)
        {
            t1Exception = e;
        }

        try
        {
            if (JsonSerializer.Deserialize<T2>(ref reader, options) is { } t2)
            {
                return t2;
            }
        }
        catch (JsonException e)
        {
            t2Exception = e;
        }

        throw new JsonException(
            $"Failed to deserialize OneOf<{typeof(T1)}, {typeof(T2)}> as either {typeof(T1)} or {typeof(T2)}",
            new AggregateException([t1Exception, t2Exception])
        );
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OneOf<T1, T2> value, JsonSerializerOptions options)
    {
        if (value.IsT0)
        {
            JsonSerializer.Serialize(writer, value.AsT0, options);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.AsT1, options);
        }
    }
}
