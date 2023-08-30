using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

[JsonSerializable(typeof(NamedComfyNode))]
public record NamedComfyNode([property: JsonIgnore] string Name) : ComfyNode, IOutputNode
{
    /// <summary>
    /// Returns { Name, index } for use as a node connection
    /// </summary>
    public object[] GetOutput(int index)
    {
        return new object[] { Name, index };
    }
    
    /// <summary>
    /// Returns typed { Name, index } for use as a node connection
    /// </summary>
    public TOutput GetOutput<TOutput>(int index) where TOutput : NodeConnectionBase, new()
    {
        return new TOutput
        {
            Data = GetOutput(index)
        };
    }
}

[JsonSerializable(typeof(NamedComfyNode<>))]
public record NamedComfyNode<TOutput>(string Name) : NamedComfyNode(Name) where TOutput : NodeConnectionBase, new()
{
    public TOutput Output => new TOutput
    {
        Data = GetOutput(0)
    };
}

[JsonSerializable(typeof(NamedComfyNode<>))]
public record NamedComfyNode<TOutput, TOutput2>(string Name) : NamedComfyNode(Name) 
    where TOutput : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
{
    public TOutput Output => new TOutput
    {
        Data = GetOutput(0)
    };
    
    public TOutput2 Output2 => new TOutput2
    {
        Data = GetOutput(1)
    };
}
