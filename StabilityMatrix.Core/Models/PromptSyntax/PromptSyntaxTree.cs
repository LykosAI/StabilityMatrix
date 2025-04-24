using System.Text;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Core.Models.PromptSyntax;

public class PromptSyntaxTree(string sourceText, DocumentNode rootNode, IReadOnlyList<IToken> tokens)
{
    public DocumentNode RootNode { get; } = rootNode;

    public IReadOnlyList<IToken> Tokens { get; } = tokens;

    public string SourceText { get; } = sourceText;

    // Get source text for a specific node
    public string GetSourceText(PromptNode node)
    {
        return GetSourceText(node.Span);
    }

    // Get source text for a specific node
    public string GetSourceText(TextSpan span)
    {
        if (span.Start < 0)
            throw new ArgumentOutOfRangeException(nameof(span), "Node indices are out of range.");

        // Trim length if it exceeds the source text length
        var length = span.End > SourceText.Length ? SourceText.Length - span.Start : span.Length;

        return SourceText.Substring(span.Start, length);
    }

    public string ToDebugString()
    {
        var sb = new StringBuilder();
        foreach (var node in RootNode.Content)
        {
            AppendNode(node, sb, 0);
        }
        return sb.ToString();
    }

    private void AppendNode(PromptNode node, StringBuilder sb, int indentLevel)
    {
        sb.Append(' ', indentLevel * 4); // 4 spaces per indent level
        sb.Append("- ");

        switch (node)
        {
            case TextNode textNode:
                sb.AppendLine(
                    $"TextNode: \"{textNode.Text.Replace("\n", "\\n")}\" ({textNode.StartIndex}-{textNode.EndIndex})"
                ); // Escape newlines
                break;
            case ParenthesizedNode parenNode:
                sb.AppendLine($"ParenthesizedNode: ({parenNode.StartIndex}-{parenNode.EndIndex})");
                foreach (var child in parenNode.Content)
                {
                    AppendNode(child, sb, indentLevel + 1);
                }
                if (parenNode.Weight != null)
                {
                    AppendNode(parenNode.Weight, sb, indentLevel + 1);
                }
                break;
            case NetworkNode networkNode:
                sb.AppendLine(
                    $"NetworkNode: Type={networkNode.NetworkType}, Model={networkNode.ModelName}, Weight={networkNode.ModelWeight}, ClipWeight={networkNode.ClipWeight} ({networkNode.StartIndex}-{networkNode.EndIndex})"
                );
                break;
            case WildcardNode wildcardNode:
                sb.AppendLine($"WildcardNode: ({node.StartIndex}-{node.EndIndex})");
                foreach (var option in wildcardNode.Options)
                {
                    AppendNode(option, sb, indentLevel + 1);
                }
                break;
            case CommentNode commentNode:
                sb.AppendLine($"CommentNode: \"{commentNode.Text}\" ({node.StartIndex}-{node.EndIndex})");
                break;

            case NumberNode numberNode:
                sb.AppendLine($"NumberNode: \"{numberNode.Value}\" ({node.StartIndex}-{node.EndIndex})");
                break;

            case KeywordNode keywordNode:
                sb.AppendLine($"KeywordNode: \"{keywordNode.Keyword}\" ({node.StartIndex}-{node.EndIndex})");
                break;
            case ArrayNode arrayNode:
                sb.AppendLine($"ArrayNode: ({node.StartIndex}-{node.EndIndex})");
                foreach (var child in arrayNode.Elements)
                {
                    AppendNode(child, sb, indentLevel + 1);
                }
                break;

            // Add cases for other node types...
            default:
                sb.AppendLine(
                    $"Unknown Node Type: {node.GetType().Name} ({node.StartIndex}-{node.EndIndex})"
                );
                break;
        }
    }
}
