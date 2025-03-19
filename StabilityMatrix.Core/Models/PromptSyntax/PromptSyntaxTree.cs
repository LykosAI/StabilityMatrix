using TextMateSharp.Grammars;

namespace StabilityMatrix.Core.Models.PromptSyntax;

public class PromptSyntaxTree(string sourceText, List<PromptNode> rootNodes, List<IToken> tokens)
{
    public List<PromptNode> RootNodes { get; } = rootNodes;

    public List<IToken> Tokens { get; } = tokens;

    public string SourceText { get; } = sourceText;

    // Get source text for a specific node
    public string GetSourceText(PromptNode node)
    {
        return GetSourceText(node.Span);
    }

    // Get source text for a specific node
    public string GetSourceText(TextSpan span)
    {
        if (span.Start < 0 || span.End > SourceText.Length)
            throw new ArgumentOutOfRangeException(nameof(span), "Node indices are out of range.");

        return SourceText.Substring(span.Start, span.Length);
    }

    // Helper method to find all nodes within a given segment
    public List<PromptNode> FindNodesInSegment(int startOffset, int endOffset)
    {
        var foundNodes = new List<PromptNode>();
        FindNodesInSegmentRecursive(RootNodes, startOffset, endOffset, foundNodes);
        return foundNodes;
    }

    private void FindNodesInSegmentRecursive(
        IEnumerable<PromptNode> nodes,
        int startOffset,
        int endOffset,
        List<PromptNode> foundNodes
    )
    {
        foreach (var node in nodes)
        {
            // Use the StartIndex and EndIndex from the node itself
            if (node.EndIndex > startOffset && node.StartIndex < endOffset)
            {
                foundNodes.Add(node);
            }

            // Recursively check children of container nodes
            if (node is ParenthesizedNode parenthesizedNode)
            {
                if (parenthesizedNode.Content?.Count > 0)
                {
                    FindNodesInSegmentRecursive(
                        parenthesizedNode.Content,
                        startOffset,
                        endOffset,
                        foundNodes
                    );
                }
                if (parenthesizedNode.Weight != null)
                {
                    foundNodes.Add(parenthesizedNode.Weight);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                FindNodesInSegmentRecursive(arrayNode.Elements, startOffset, endOffset, foundNodes);
            }
            else if (node is WildcardNode wildcardNode)
            {
                foreach (var option in wildcardNode.Options)
                {
                    FindNodesInSegmentRecursive(
                        new List<PromptNode> { option },
                        startOffset,
                        endOffset,
                        foundNodes
                    ); // Wrap in a list
                }
            }
            // Add other container node types as needed.
        }
    }
}
