using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public class NodeDictionary : Dictionary<string, ComfyNode>
{
    /// <summary>
    /// Tracks base names and their highest index resulting from <see cref="GetUniqueName"/>
    /// </summary>
    private readonly Dictionary<string, int> _baseNameIndex = new();

    /// <summary>
    /// Finds a unique node name given a base name, by appending _2, _3, etc.
    /// </summary>
    public string GetUniqueName(string nameBase)
    {
        if (_baseNameIndex.TryGetValue(nameBase, out var index))
        {
            var newIndex = checked(index + 1);
            _baseNameIndex[nameBase] = newIndex;
            return $"{nameBase}_{newIndex}";
        }

        // Ensure new name does not exist
        const int initialIndex = 1;
        var name = $"{nameBase}_{initialIndex}";

        if (ContainsKey(name))
        {
            throw new InvalidOperationException(
                $"Initial unique name {name} already exists for base {nameBase}"
            );
        }

        _baseNameIndex.Add(nameBase, initialIndex);

        return name;
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
        Add(node.Name, node);
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
                }
            }
        }
    }
}
