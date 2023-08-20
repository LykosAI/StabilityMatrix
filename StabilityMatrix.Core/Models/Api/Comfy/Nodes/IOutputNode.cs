namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public interface IOutputNode
{
    /// <summary>
    /// Returns { Name, index } for use as a node connection
    /// </summary>
    public object[] GetOutput(int index);
}
