using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using Yoh.Text.Json.NamingPolicies;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public abstract record ComfyTypedNodeBase
{
    [Localizable(false)]
    protected virtual string ClassType
    {
        get
        {
            var type = GetType();

            // Use options name if available
            if (type.GetCustomAttribute<TypedNodeOptionsAttribute>() is { } options)
            {
                if (!string.IsNullOrEmpty(options.Name))
                {
                    return options.Name;
                }
            }

            // Otherwise use class name
            return type.Name;
        }
    }

    [Localizable(false)]
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

            // If theres a BoolStringMember attribute, convert to one of the strings
            if (property.GetCustomAttribute<BoolStringMemberAttribute>() is { } converter)
            {
                if (value is bool boolValue)
                {
                    inputs.Add(key, boolValue ? converter.TrueString : converter.FalseString);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Property {property.Name} is not a bool, but has a BoolStringMember attribute"
                    );
                }

                continue;
            }

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
    public TOutput2 Output2 => new() { Data = new object[] { Name, 1 } };

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
    public TOutput2 Output2 => new() { Data = new object[] { Name, 1 } };

    [JsonIgnore]
    public TOutput3 Output3 => new() { Data = new object[] { Name, 2 } };

    public static implicit operator NamedComfyNode<TOutput1, TOutput2, TOutput3>(
        ComfyTypedNodeBase<TOutput1, TOutput2, TOutput3> node
    ) => (NamedComfyNode<TOutput1, TOutput2, TOutput3>)node.ToNamedNode();
}

public abstract record ComfyTypedNodeBase<TOutput1, TOutput2, TOutput3, TOutput4> : ComfyTypedNodeBase
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
    where TOutput3 : NodeConnectionBase, new()
    where TOutput4 : NodeConnectionBase, new()
{
    [JsonIgnore]
    public TOutput1 Output1 => new() { Data = new object[] { Name, 0 } };

    [JsonIgnore]
    public TOutput2 Output2 => new() { Data = new object[] { Name, 1 } };

    [JsonIgnore]
    public TOutput3 Output3 => new() { Data = new object[] { Name, 2 } };

    [JsonIgnore]
    public TOutput4 Output4 => new() { Data = new object[] { Name, 3 } };

    public static implicit operator NamedComfyNode<TOutput1, TOutput2, TOutput3, TOutput4>(
        ComfyTypedNodeBase<TOutput1, TOutput2, TOutput3, TOutput4> node
    ) => (NamedComfyNode<TOutput1, TOutput2, TOutput3, TOutput4>)node.ToNamedNode();
}

public abstract record ComfyTypedNodeBase<TOutput1, TOutput2, TOutput3, TOutput4, TOutput5>
    : ComfyTypedNodeBase
    where TOutput1 : NodeConnectionBase, new()
    where TOutput2 : NodeConnectionBase, new()
    where TOutput3 : NodeConnectionBase, new()
    where TOutput4 : NodeConnectionBase, new()
    where TOutput5 : NodeConnectionBase, new()
{
    [JsonIgnore]
    public TOutput1 Output1 => new() { Data = new object[] { Name, 0 } };

    [JsonIgnore]
    public TOutput2 Output2 => new() { Data = new object[] { Name, 1 } };

    [JsonIgnore]
    public TOutput3 Output3 => new() { Data = new object[] { Name, 2 } };

    [JsonIgnore]
    public TOutput4 Output4 => new() { Data = new object[] { Name, 3 } };

    [JsonIgnore]
    public TOutput5 Output5 => new() { Data = new object[] { Name, 4 } };

    public static implicit operator NamedComfyNode<TOutput1, TOutput2, TOutput3, TOutput4, TOutput5>(
        ComfyTypedNodeBase<TOutput1, TOutput2, TOutput3, TOutput4, TOutput5> node
    ) => (NamedComfyNode<TOutput1, TOutput2, TOutput3, TOutput4, TOutput5>)node.ToNamedNode();
}
