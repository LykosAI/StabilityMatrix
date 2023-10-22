using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public class NodeDictionary : Dictionary<string, ComfyNode>
{
    public string GetUniqueName(string nameBase)
    {
        var name = nameBase;

        for (var i = 0; ContainsKey(name); i++)
        {
            if (i > 1_000_000)
            {
                throw new InvalidOperationException(
                    $"Could not find unique name for base {nameBase}"
                );
            }

            name = $"{nameBase}_{i + 2}";
        }

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
