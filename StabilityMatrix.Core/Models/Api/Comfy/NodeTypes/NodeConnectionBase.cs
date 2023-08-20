using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

[JsonConverter(typeof(NodeConnectionBaseJsonConverter))]
public abstract class NodeConnectionBase
{
    public object[]? Data { get; set; }

    // Implicit conversion to object[]
    public static implicit operator object[](NodeConnectionBase nodeConnection)
    {
        return nodeConnection.Data ?? Array.Empty<object>();
    }
}
