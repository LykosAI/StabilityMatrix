namespace StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

public abstract class NodeConnectionBase
{
    /// <summary>
    /// Array data for the connection.
    /// [(string) Node Name, (int) Connection Index]
    /// </summary>
    public object[]? Data { get; init; }
}
