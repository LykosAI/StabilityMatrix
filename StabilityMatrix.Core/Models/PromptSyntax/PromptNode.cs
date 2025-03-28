using System.Diagnostics;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.PromptSyntax;

/// <summary>
/// Interface for nodes that can have children.
/// </summary>
public interface IHasChildren
{
    IEnumerable<PromptNode> Children { get; }
}

[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
public abstract class PromptNode
{
    public PromptNode? Parent { get; set; }

    public int StartIndex { get; set; }

    public int EndIndex
    {
        get => StartIndex + Length;
        set => Length = value - StartIndex;
    }

    public int Length { get; set; }

    public required TextSpan Span
    {
        get => new(StartIndex, Length);
        set => (StartIndex, Length) = (value.Start, value.Length);
    }

    /// <summary>
    /// Gets a list of ancestor nodes
    /// </summary>
    public IEnumerable<PromptNode> Ancestors()
    {
        return Parent?.AncestorsAndSelf() ?? [];
    }

    /// <summary>
    /// Gets a list of ancestor nodes (including this node)
    /// </summary>
    public IEnumerable<PromptNode> AncestorsAndSelf()
    {
        for (var node = this; node != null; node = Parent)
        {
            yield return node;
        }
    }

    /// <summary>
    /// Gets a list of descendant nodes.
    /// </summary>
    /// <param name="descendIntoChildren">Determines if the search descends into a node's children.</param>
    public IEnumerable<PromptNode> DescendantNodes(Func<PromptNode, bool>? descendIntoChildren = null)
    {
        if (this is not IHasChildren hasChildren)
            yield break;

        foreach (var child in hasChildren.Children)
        {
            yield return child;

            if (descendIntoChildren == null || descendIntoChildren(child))
            {
                foreach (var descendant in child.DescendantNodes(descendIntoChildren))
                {
                    yield return descendant;
                }
            }
        }
    }

    /// <summary>
    /// Gets a list of descendant nodes.
    /// </summary>
    /// <param name="span">The span the node's full span must intersect.</param>
    /// <param name="descendIntoChildren">Determines if the search descends into a node's children.</param>
    public IEnumerable<PromptNode> DescendantNodes(
        TextSpan span,
        Func<PromptNode, bool>? descendIntoChildren = null
    )
    {
        if (this is not IHasChildren hasChildren)
            yield break;

        foreach (var child in hasChildren.Children)
        {
            // Stop if exceeded
            if (child.StartIndex > span.End)
                break;

            // Check span
            if (!child.Span.IntersectsWith(span))
                continue;

            yield return child;

            // Check if we should descend into children
            if (descendIntoChildren != null && !descendIntoChildren(child))
                continue;

            foreach (var descendant in child.DescendantNodes(span, descendIntoChildren))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Finds the descendant node with the smallest span that completely contains the provided target span.
    /// Returns null if this node does not contain the target span.
    /// </summary>
    public PromptNode FindSmallestContainingDescendant(TextSpan span)
    {
        // Ensure the current node contains the target span
        if (!Span.Contains(span))
        {
            throw new ArgumentOutOfRangeException(
                nameof(span),
                $"Node span {Span} does not contain the target span {span}"
            );
        }

        var bestMatch = this;

        // Iterate through all descendant nodes
        foreach (var descendant in DescendantNodes())
        {
            // Check if descendant fully contains the target span
            if (descendant.StartIndex <= span.Start && descendant.EndIndex >= span.End)
            {
                // Select this descendant if its span is smaller than the current best match
                if (descendant.Length < bestMatch.Length)
                {
                    bestMatch = descendant;
                }
            }
        }

        return bestMatch;
    }

    public override string ToString()
    {
        return $"{GetType().Name} {Span}";
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }
}

public class DocumentNode : PromptNode, IHasChildren
{
    public List<PromptNode> Content { get; init; } = [];

    IEnumerable<PromptNode> IHasChildren.Children => Content;
}

public class IdentifierNode : PromptNode
{
    public required string Name { get; set; }
}

public class LiteralNode<T> : PromptNode
{
    public required string Raw { get; init; }

    public required T Value { get; init; }
}

public class TextNode : PromptNode
{
    public required string Text { get; set; }
}

public class SeperatorNode : TextNode;

public class NumberNode : LiteralNode<decimal>;

public class ParenthesizedNode : PromptNode, IHasChildren
{
    public List<PromptNode> Content { get; } = [];
    public NumberNode? Weight { get; set; }

    IEnumerable<PromptNode> IHasChildren.Children => Content.AppendIfNotNull(Weight);
}

public class ArrayNode : PromptNode, IHasChildren
{
    public List<PromptNode> Elements { get; } = [];

    IEnumerable<PromptNode> IHasChildren.Children => Elements;
}

public class NetworkNode : PromptNode, IHasChildren
{
    public required IdentifierNode NetworkType { get; init; }
    public required TextNode ModelName { get; init; }
    public NumberNode? ModelWeight { get; set; }
    public NumberNode? ClipWeight { get; set; }

    IEnumerable<PromptNode> IHasChildren.Children =>
        new List<PromptNode> { NetworkType, ModelName }
            .AppendIfNotNull(ModelWeight)
            .AppendIfNotNull(ClipWeight);
}

public class WildcardNode : PromptNode, IHasChildren
{
    public List<PromptNode> Options { get; } = [];

    IEnumerable<PromptNode> IHasChildren.Children => Options;
}

public class CommentNode : PromptNode
{
    public string? Text { get; set; }
}

public class KeywordNode : PromptNode // AND, BREAK
{
    public required string Keyword { get; set; }
}

// Add other node types as needed (e.g., SeparatorNode, EscapeNode, etc.)
