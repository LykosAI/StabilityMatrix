namespace StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

public abstract class NodeConnectionBase
{
    public object[]? Data { get; set; }

    // Implicit conversion to object[]
    public static implicit operator object[](NodeConnectionBase nodeConnection)
    {
        return nodeConnection.Data ?? Array.Empty<object>();
    }
}
