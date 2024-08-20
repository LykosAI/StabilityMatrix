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
    public TOutput GetOutput<TOutput>(int index)
        where TOutput : NodeConnectionBase, new()
    {
        return new TOutput { Data = GetOutput(index) };
    }
}

[JsonSerializable(typeof(NamedComfyNode<>))]
public record NamedComfyNode<TOutput>(string Name) : NamedComfyNode(Name)
    where TOutput : NodeConnectionBase, new()
{
    public TOutput Output => new TOutput { Data = GetOutput(0) };
}

[JsonSerializable(typeof(NamedComfyNode<>))]
public record NamedComfyNode<TOutput1, TOutput2>(string Name) : NamedComfyNode(Name)
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
{
    public TOutput1 Output1 => new() { Data = GetOutput(0) };

    public TOutput2 Output2 => new() { Data = GetOutput(1) };
}

[JsonSerializable(typeof(NamedComfyNode<>))]
public record NamedComfyNode<TOutput1, TOutput2, TOutput3>(string Name) : NamedComfyNode(Name)
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
    where TOutput3 : NodeConnectionBase, new()
{
    public TOutput1 Output1 => new() { Data = GetOutput(0) };

    public TOutput2 Output2 => new() { Data = GetOutput(1) };

    public TOutput3 Output3 => new() { Data = GetOutput(2) };
}

[JsonSerializable(typeof(NamedComfyNode<>))]
public record NamedComfyNode<TOutput1, TOutput2, TOutput3, TOutput4>(string Name) : NamedComfyNode(Name)
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
    where TOutput3 : NodeConnectionBase, new()
    where TOutput4 : NodeConnectionBase, new()
{
    public TOutput1 Output1 => new() { Data = GetOutput(0) };
    public TOutput2 Output2 => new() { Data = GetOutput(1) };
    public TOutput3 Output3 => new() { Data = GetOutput(2) };
    public TOutput4 Output4 => new() { Data = GetOutput(3) };
}

[JsonSerializable(typeof(NamedComfyNode<>))]
public record NamedComfyNode<TOutput1, TOutput2, TOutput3, TOutput4, TOutput5>(string Name)
    : NamedComfyNode(Name)
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
    where TOutput3 : NodeConnectionBase, new()
    where TOutput4 : NodeConnectionBase, new()
    where TOutput5 : NodeConnectionBase, new()
{
    public TOutput1 Output1 => new() { Data = GetOutput(0) };
    public TOutput2 Output2 => new() { Data = GetOutput(1) };
    public TOutput3 Output3 => new() { Data = GetOutput(2) };
    public TOutput4 Output4 => new() { Data = GetOutput(3) };
    public TOutput5 Output5 => new() { Data = GetOutput(4) };
}
