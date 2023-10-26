using System.Reflection;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using Yoh.Text.Json.NamingPolicies;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public abstract record ComfyTypedNodeBase
{
    protected virtual string ClassType => GetType().Name;

    [JsonIgnore]
    public required string Name { get; init; }

    protected NamedComfyNode ToNamedNode()
    {
        var inputs = new Dictionary<string, object?>();

        // Loop through all properties, key is property name as snake_case, or JsonPropertyName
        var namingPolicy = JsonNamingPolicies.SnakeCaseLower;

        foreach (var property in GetType().GetProperties())
        {
            if (property.Name == nameof(Name) || property.GetValue(this) is not { } value)
                continue;

            // Skip JsonIgnore
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                continue;

            var key =
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? namingPolicy.ConvertName(property.Name);

            // For connection types, use data property
            if (value is NodeConnectionBase connection)
            {
                inputs.Add(key, connection.Data);
            }
            else
            {
                inputs.Add(key, value);
            }
        }

        return new NamedComfyNode(Name) { ClassType = ClassType, Inputs = inputs };
    }

    // Implicit conversion to NamedComfyNode
    public static implicit operator NamedComfyNode(ComfyTypedNodeBase node) => node.ToNamedNode();
}

public abstract record ComfyTypedNodeBase<TOutput> : ComfyTypedNodeBase
    where TOutput : NodeConnectionBase, new()
{
    [JsonIgnore]
    public TOutput Output => new() { Data = new object[] { Name, 0 } };

    public static implicit operator NamedComfyNode<TOutput>(ComfyTypedNodeBase<TOutput> node) =>
        (NamedComfyNode<TOutput>)node.ToNamedNode();
}

public abstract record ComfyTypedNodeBase<TOutput1, TOutput2> : ComfyTypedNodeBase
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
{
    [JsonIgnore]
    public TOutput1 Output1 => new() { Data = new object[] { Name, 0 } };

    [JsonIgnore]
    public TOutput1 Output2 => new() { Data = new object[] { Name, 1 } };

    public static implicit operator NamedComfyNode<TOutput1, TOutput2>(
        ComfyTypedNodeBase<TOutput1, TOutput2> node
    ) => (NamedComfyNode<TOutput1, TOutput2>)node.ToNamedNode();
}

public abstract record ComfyTypedNodeBase<TOutput1, TOutput2, TOutput3> : ComfyTypedNodeBase
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
    where TOutput3 : NodeConnectionBase, new()
{
    [JsonIgnore]
    public TOutput1 Output1 => new() { Data = new object[] { Name, 0 } };

    [JsonIgnore]
    public TOutput1 Output2 => new() { Data = new object[] { Name, 1 } };

    [JsonIgnore]
    public TOutput1 Output3 => new() { Data = new object[] { Name, 2 } };

    public static implicit operator NamedComfyNode<TOutput1, TOutput2, TOutput3>(
        ComfyTypedNodeBase<TOutput1, TOutput2, TOutput3> node
    ) => (NamedComfyNode<TOutput1, TOutput2, TOutput3>)node.ToNamedNode();
}
