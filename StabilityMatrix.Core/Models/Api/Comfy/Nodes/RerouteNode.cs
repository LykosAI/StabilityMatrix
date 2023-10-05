namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

/// <summary>
/// Skeleton node that relays the output of another node
/// </summary>
public record RerouteNode(object[] Connection) : IOutputNode
{
    /// <inheritdoc />
    public object[] GetOutput(int index)
    {
        if (index != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return Connection;
    }
}
