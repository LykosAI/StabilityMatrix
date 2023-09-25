using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public class NodeDictionary : Dictionary<string, ComfyNode>
{
    public TNamedNode AddNamedNode<TNamedNode>(TNamedNode node) where TNamedNode : NamedComfyNode
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
