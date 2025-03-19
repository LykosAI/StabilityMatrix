using System.Globalization;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Core.Models.PromptSyntax;

public class PromptSyntaxBuilder(ITokenizeLineResult tokenizeResult, string sourceText)
{
    private int currentTokenIndex;

    public PromptSyntaxTree BuildAST()
    {
        var nodes = new List<PromptNode>();

        while (MoreTokens())
        {
            nodes.Add(ParseNode());
        }

        // Set parents
        foreach (var node in nodes)
        {
            SetParents(node);
        }

        return new PromptSyntaxTree(sourceText, nodes, tokenizeResult.Tokens.ToList());
    }

    private static void SetParents(PromptNode node)
    {
        if (node is not IHasChildren hasChildren)
            return;

        foreach (var child in hasChildren.Children)
        {
            child.Parent = node;
            SetParents(child);
        }
    }

    private string GetTextSubstring(IToken token)
    {
        // IMPORTANT TextMate notes:
        // 1. IToken.EndIndex is exclusive
        // 2. Last token may exceed the length of the string, (length 10 string has EndIndex = 11)
        var length =
            token.EndIndex > sourceText.Length
                ? sourceText.Length - token.StartIndex
                : token.EndIndex - token.StartIndex;

        return sourceText.Substring(token.StartIndex, length);
    }

    private PromptNode ParseNode()
    {
        var token = PeekToken(); // Look at the next token without consuming it.

        if (token is null)
        {
            throw new InvalidOperationException("Unexpected end of input.");
        }

        if (token.Scopes.Contains("comment.line.number-sign.prompt"))
        {
            return ParseComment();
        }
        else if (
            token.Scopes.Contains("meta.structure.wildcard.prompt")
            && token.Scopes.Contains("punctuation.definition.wildcard.begin.prompt")
        )
        {
            return ParseWildcard();
        }
        else if (
            token.Scopes.Contains("meta.structure.array.prompt")
            && token.Scopes.Contains("punctuation.definition.array.begin.prompt")
        )
        {
            return ParseParenthesized();
        }
        else if (
            token.Scopes.Contains("meta.structure.array.prompt")
            && token.Scopes.Contains("punctuation.definition.array.begin.prompt")
        )
        {
            return ParseArray();
        }
        else if (
            token.Scopes.Contains("meta.structure.network.prompt")
            && token.Scopes.Contains("punctuation.definition.network.begin.prompt")
        )
        {
            return ParseNetwork();
        }
        else if (token.Scopes.Contains("keyword.control"))
        {
            return ParseKeyword();
        }
        else if (token.Scopes.Contains("meta.embedded"))
        {
            return ParseText();
        }
        else
        {
            // Handle other token types (separator, escape, etc.)
            // Or throw an exception for unexpected tokens.
            return ParseText();
        }
    }

    private CommentNode ParseComment()
    {
        var token = ConsumeToken();
        var text = GetTextSubstring(token);
        return new CommentNode { Span = new TextSpan(token.StartIndex, token.Length), Text = text };
    }

    private IdentifierNode ParseIdentifier()
    {
        var token = ConsumeToken();
        var text = GetTextSubstring(token);
        return new IdentifierNode { Span = new TextSpan(token.StartIndex, token.Length), Name = text };
    }

    private TextNode ParseText()
    {
        var token = ConsumeToken(); // Consume the text token.
        var text = GetTextSubstring(token);
        return new TextNode { Span = new TextSpan(token.StartIndex, token.Length), Text = text };
    }

    private KeywordNode ParseKeyword()
    {
        var token = ConsumeToken();
        var keyword = GetTextSubstring(token);
        return new KeywordNode { Span = new TextSpan(token.StartIndex, token.Length), Keyword = keyword };
    }

    private NumberNode ParseNumber()
    {
        var token = ConsumeToken();
        var number = GetTextSubstring(token);

        return new NumberNode
        {
            Raw = number,
            Span = new TextSpan(token.StartIndex, token.Length),
            Value = decimal.Parse(number, CultureInfo.InvariantCulture)
        };
    }

    private ParenthesizedNode ParseParenthesized()
    {
        var openParenToken = ConsumeToken(); // Consume the '('
        if (
            openParenToken is null
            || !openParenToken.Scopes.Contains("punctuation.definition.array.begin.prompt")
        )
            throw new InvalidOperationException("Expected opening parenthesis.");

        // Set start index
        var node = new ParenthesizedNode { Span = new TextSpan(openParenToken.StartIndex, 0) };

        while (MoreTokens())
        {
            if (PeekToken() is not { } nextToken)
                break;

            if (nextToken.Scopes.Contains("punctuation.separator.weight.prompt"))
            {
                // Parse the weight.
                ConsumeToken(); // Consume the ':'

                // Check the weight value token.
                var weightToken = PeekToken();
                if (weightToken is null || !weightToken.Scopes.Contains("constant.numeric"))
                {
                    throw new InvalidOperationException("Expected numeric weight value.");
                }

                // Consume the weight token.
                node.Weight = ParseNumber();
            }
            // We're supposed to check `punctuation.definition.array.end.prompt` here, textmate is not parsing it
            // separately with current tmLanguage grammar, so use `meta.structure.weight.prompt` for now
            // We check this AFTER `punctuation.separator.weight.prompt` to avoid consuming the ':'
            else if (nextToken.Scopes.Contains("meta.structure.weight.prompt"))
            {
                ConsumeToken(); // Consume the ')'
                node.EndIndex = nextToken.EndIndex; // Set end index
                break;
            }
            else
            {
                // It's part of the content.
                node.Content.Add(ParseNode()); // Recursively parse nested nodes.
            }
        }

        return node;
    }

    private NetworkNode ParseNetwork()
    {
        var beginNetworkToken = ConsumeToken();
        if (
            beginNetworkToken is null
            || !beginNetworkToken.Scopes.Contains("punctuation.definition.network.begin.prompt")
        )
            throw new InvalidOperationException("Expected opening bracket.");

        // type
        var typeToken = PeekToken();
        if (typeToken is null || !typeToken.Scopes.Contains("meta.embedded.network.type.prompt"))
            throw new InvalidOperationException("Expected network type.");
        var type = ParseIdentifier();

        // colon
        var colonToken = ConsumeToken();
        if (colonToken is null || !colonToken.Scopes.Contains("punctuation.separator.variable.prompt"))
            throw new InvalidOperationException("Expected colon.");

        // name
        var nameToken = PeekToken();
        if (nameToken is null || !nameToken.Scopes.Contains("meta.embedded.network.model.prompt"))
            throw new InvalidOperationException("Expected network name.");
        var name = ParseText();

        // model weight, clip weight
        NumberNode? modelWeight = null;
        NumberNode? clipWeight = null;

        // colon
        var nextToken = PeekToken();
        if (nextToken is not null && nextToken.Scopes.Contains("punctuation.separator.variable.prompt"))
        {
            ConsumeToken(); // consume colon

            // Parse the model weight.
            var modelWeightToken = ConsumeToken();
            if (modelWeightToken is null || !modelWeightToken.Scopes.Contains("constant.numeric"))
                throw new InvalidOperationException("Expected network weight.");
            modelWeight = ParseNumber();

            // colon
            nextToken = PeekToken();
            if (nextToken is not null && nextToken.Scopes.Contains("punctuation.separator.variable.prompt"))
            {
                ConsumeToken(); // consume colon

                // Parse the clip weight.
                var clipWeightToken = ConsumeToken();
                if (clipWeightToken is null || !clipWeightToken.Scopes.Contains("constant.numeric"))
                    throw new InvalidOperationException("Expected network weight.");
                clipWeight = ParseNumber();
            }
        }

        var endNetworkToken = ConsumeToken();
        if (
            endNetworkToken is null
            || !endNetworkToken.Scopes.Contains("punctuation.definition.network.end.prompt")
        )
            throw new InvalidOperationException("Expected closing bracket.");

        return new NetworkNode
        {
            Span = TextSpan.FromBounds(beginNetworkToken.StartIndex, endNetworkToken.EndIndex),
            NetworkType = type,
            ModelName = name,
            ModelWeight = modelWeight,
            ClipWeight = clipWeight
        };
    }

    private ArrayNode ParseArray()
    {
        var openBracket = ConsumeToken();
        if (openBracket is null || !openBracket.Scopes.Contains("punctuation.definition.array.begin.prompt"))
            throw new InvalidOperationException("Expected opening bracket.");

        var node = new ArrayNode
        {
            Span = new TextSpan(openBracket.StartIndex, 0) // Set start index
        };

        while (MoreTokens())
        {
            var nextToken = PeekToken();
            if (nextToken is null)
                break;

            if (nextToken.Scopes.Contains("punctuation.definition.array.end.prompt"))
            {
                ConsumeToken(); // Consume the ']'
                node.EndIndex = nextToken.EndIndex; //Set end index
                break;
            }
            else
            {
                node.Elements.Add(ParseNode()); // Recursively parse nested nodes.
            }
        }

        return node;
    }

    private WildcardNode ParseWildcard()
    {
        var openBraceToken = ConsumeToken(); // Consume the '{'
        if (
            openBraceToken is null
            || !openBraceToken.Scopes.Contains("punctuation.definition.wildcard.begin.prompt")
        )
            throw new InvalidOperationException("Expected opening brace.");

        var node = new WildcardNode
        {
            Span = new TextSpan(openBraceToken.StartIndex, 0) // Set start index
        };

        while (MoreTokens())
        {
            var nextToken = PeekToken();
            if (nextToken is null)
                break;

            if (nextToken.Scopes.Contains("punctuation.definition.wildcard.end.prompt"))
            {
                ConsumeToken(); // Consume the '}'
                node.EndIndex = nextToken.EndIndex;
                break;
            }
            else if (nextToken.Scopes.Contains("keyword.operator.choice.prompt"))
            {
                ConsumeToken(); // Consume the '|'
            }
            else
            {
                node.Options.Add(ParseNode()); // Recursively parse nested nodes.
            }
        }

        return node;
    }

    private IToken? PeekToken()
    {
        if (currentTokenIndex < tokenizeResult.Tokens.Length)
        {
            return tokenizeResult.Tokens[currentTokenIndex];
        }
        return null;
    }

    private IToken ConsumeToken()
    {
        if (!MoreTokens())
            throw new InvalidOperationException("No more tokens to consume.");

        return tokenizeResult.Tokens[currentTokenIndex++];
    }

    private bool MoreTokens()
    {
        return currentTokenIndex < tokenizeResult.Tokens.Length;
    }
}
