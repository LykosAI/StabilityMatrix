using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using KGySoft.CoreLibraries;
using OneOf;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public class NodeDictionary : Dictionary<string, ComfyNode>
{
    /// <summary>
    /// Tracks base names and their highest index resulting from <see cref="GetUniqueName"/>
    /// </summary>
    private readonly Dictionary<string, int> _baseNameIndex = new();

    /// <summary>
    /// When inserting TypedNodes, this holds a mapping of ClassType to required extensions
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, ExtensionSpecifier[]> ClassTypeRequiredExtensions { get; } = new();

    public IEnumerable<ExtensionSpecifier> RequiredExtensions =>
        ClassTypeRequiredExtensions.Values.SelectMany(x => x);

    /// <summary>
    /// Finds a unique node name given a base name, by appending _2, _3, etc.
    /// </summary>
    public string GetUniqueName([Localizable(false)] string nameBase)
    {
        if (_baseNameIndex.TryGetValue(nameBase, out var index))
        {
            var newIndex = checked(index + 1);
            _baseNameIndex[nameBase] = newIndex;
            return $"{nameBase}_{newIndex}";
        }

        // Ensure new name does not exist
        if (ContainsKey(nameBase))
        {
            throw new InvalidOperationException($"Initial unique name already exists for base {nameBase}");
        }

        _baseNameIndex.Add(nameBase, 1);

        return nameBase;
    }

    public TNamedNode AddNamedNode<TNamedNode>(TNamedNode node)
        where TNamedNode : NamedComfyNode
    {
        Add(node.Name, node);
        return node;
    }

    public TTypedNode AddTypedNode<TTypedNode>(TTypedNode node)
        where TTypedNode : ComfyTypedNodeBase
    {
        var namedNode = (NamedComfyNode)node;

        Add(node.Name, namedNode);

        // Check statically annotated stuff for TypedNodeOptionsAttribute
        if (node.GetType().GetCustomAttribute<TypedNodeOptionsAttribute>() is { } options)
        {
            if (options.RequiredExtensions != null)
            {
                ClassTypeRequiredExtensions.AddOrUpdate(
                    namedNode.ClassType,
                    _ => options.GetRequiredExtensionSpecifiers().ToArray(),
                    (_, specifiers) => options.GetRequiredExtensionSpecifiers().Concat(specifiers).ToArray()
                );
            }
        }

        return node;
    }

    public void NormalizeConnectionTypes()
    {
        using var _ = new CodeTimer();

        // Convert all node inputs containing NodeConnectionBase objects to their Data property
        foreach (var node in Values)
        {
            lock (node.Inputs)
            {
                foreach (var (key, input) in node.Inputs)
                {
                    if (input is NodeConnectionBase connection)
                    {
                        node.Inputs[key] = connection.Data;
                    }
                    else if (input is IOneOf { Value: NodeConnectionBase oneOfConnection })
                    {
                        node.Inputs[key] = oneOfConnection.Data;
                    }
                }
            }
        }
    }
}
