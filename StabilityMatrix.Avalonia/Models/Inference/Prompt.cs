using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Tokens;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Models.Inference;

public record Prompt
{
    public required string RawText { get; init; }

    public required ITokenizeLineResult TokenizeResult { get; init; }

    [MemberNotNullWhen(true, nameof(ExtraNetworks), nameof(ProcessedText))]
    public bool IsProcessed { get; private set; }

    /// <summary>
    /// Extra networks specified in prompt.
    /// </summary>
    public IReadOnlyList<PromptExtraNetwork>? ExtraNetworks { get; private set; }

    /// <summary>
    /// Processed text suitable for sending to inference backend.
    /// This excludes extra network (i.e. LORA) tokens.
    /// </summary>
    public string? ProcessedText { get; private set; }

    [MemberNotNull(nameof(ExtraNetworks), nameof(ProcessedText))]
    public void Process()
    {
        if (IsProcessed)
            return;

        var (promptExtraNetworks, processedText) = GetExtraNetworks();
        ExtraNetworks = promptExtraNetworks;
        ProcessedText = processedText;
    }

    private int GetSafeEndIndex(int index)
    {
        return Math.Min(index, RawText.Length);
    }

    private (List<PromptExtraNetwork> promptExtraNetworks, string processedText) GetExtraNetworks()
    {
        // Parse tokens "meta.structure.network.prompt"
        // "<": "punctuation.definition.network.begin.prompt"
        // (type): "meta.embedded.network.type.prompt"
        // ":": "punctuation.separator.variable.prompt"
        // (content): "meta.embedded.network.model.prompt"
        // ">": "punctuation.definition.network.end.prompt"
        using var tokens = TokenizeResult.Tokens.Cast<IToken>().GetEnumerator();

        // Store non-network tokens
        var outputTokens = new Stack<IToken>();
        var outputText = new Stack<string>();

        // Store extra networks
        var promptExtraNetworks = new List<PromptExtraNetwork>();

        while (tokens.MoveNext())
        {
            var token = tokens.Current;

            // For any invalid syntax, throw
            if (token.Scopes.Any(s => s.Contains("invalid.illegal")))
            {
                // Generic
                throw new PromptSyntaxError(
                    "Invalid Token",
                    token.StartIndex,
                    GetSafeEndIndex(token.EndIndex)
                );
            }

            // Find start of network token, until then just add to output
            if (!token.Scopes.Contains("punctuation.definition.network.begin.prompt"))
            {
                // Push both token and text
                outputTokens.Push(token);
                outputText.Push(RawText[token.StartIndex..GetSafeEndIndex(token.EndIndex)]);
                continue;
            }

            // Expect next token to be network type
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    token.StartIndex,
                    GetSafeEndIndex(token.EndIndex)
                );
            }
            var networkTypeToken = tokens.Current;

            if (!networkTypeToken.Scopes.Contains("meta.embedded.network.type.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedType(
                    networkTypeToken.StartIndex,
                    GetSafeEndIndex(networkTypeToken.EndIndex)
                );
            }

            var networkType = RawText[
                networkTypeToken.StartIndex..GetSafeEndIndex(networkTypeToken.EndIndex)
            ];

            // Match network type
            var parsedNetworkType = networkType switch
            {
                "lora" => PromptExtraNetworkType.Lora,
                "lyco" => PromptExtraNetworkType.LyCORIS,
                "embedding" => PromptExtraNetworkType.Embedding,
                _
                    => throw PromptValidationError.Network_UnknownType(
                        networkTypeToken.StartIndex,
                        GetSafeEndIndex(networkTypeToken.EndIndex)
                    )
            };

            // Skip colon token
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }
            // Ensure next token is colon
            if (!tokens.Current.Scopes.Contains("punctuation.separator.variable.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedSeparator(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }

            // Get model name
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }

            var modelNameToken = tokens.Current;
            if (!tokens.Current.Scopes.Contains("meta.embedded.network.model.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedName(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }

            var modelName = RawText[
                modelNameToken.StartIndex..GetSafeEndIndex(modelNameToken.EndIndex)
            ];

            // Skip another colon token
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }

            // If its a ending token instead, we can end here
            if (tokens.Current.Scopes.Contains("punctuation.definition.network.end.prompt"))
            {
                // If last entry on stack is a separator, remove it
                if (
                    outputTokens.TryPeek(out var lastToken)
                    && lastToken.Scopes.Contains("punctuation.separator.variable.prompt")
                )
                {
                    outputTokens.Pop();
                    outputText.Pop();
                }

                promptExtraNetworks.Add(
                    new PromptExtraNetwork { Type = parsedNetworkType, Name = modelName }
                );
                continue;
            }

            // Ensure next token is colon
            if (!tokens.Current.Scopes.Contains("punctuation.separator.variable.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedSeparator(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }

            // Get model weight
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }

            var modelWeightToken = tokens.Current;
            if (!tokens.Current.Scopes.Contains("constant.numeric"))
            {
                throw PromptSyntaxError.Network_ExpectedWeight(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }

            var modelWeight = RawText[
                modelWeightToken.StartIndex..GetSafeEndIndex(modelWeightToken.EndIndex)
            ];

            // Convert to double
            if (!double.TryParse(modelWeight, out var weight))
            {
                throw PromptValidationError.Network_InvalidWeight(
                    modelWeightToken.StartIndex,
                    GetSafeEndIndex(modelWeightToken.EndIndex)
                );
            }

            // Expect end
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    tokens.Current.StartIndex,
                    GetSafeEndIndex(tokens.Current.EndIndex)
                );
            }
            var endToken = tokens.Current;
            if (!endToken.Scopes.Contains("punctuation.definition.network.end.prompt"))
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    endToken.StartIndex,
                    GetSafeEndIndex(endToken.EndIndex)
                );
            }

            // If last entry on stack is a separator, remove it
            if (
                outputTokens.TryPeek(out var lastToken2)
                && lastToken2.Scopes.Contains("punctuation.separator.variable.prompt")
            )
            {
                outputTokens.Pop();
                outputText.Pop();
            }

            // Add to output
            promptExtraNetworks.Add(
                new PromptExtraNetwork
                {
                    Type = parsedNetworkType,
                    Name = modelName,
                    ModelWeight = weight
                }
            );
        }

        var processedText = string.Join("", outputText.Reverse());

        return (promptExtraNetworks, processedText);
    }

    public string GetDebugText()
    {
        var builder = new StringBuilder();

        foreach (var token in TokenizeResult.Tokens)
        {
            // Get token text
            var text = RawText[token.StartIndex..Math.Min(token.EndIndex, RawText.Length - 1)];

            // Format scope
            var scopeStr = string.Join(
                ", ",
                token.Scopes
                    .Where(s => s != "source.prompt")
                    .Select(
                        s =>
                            s.EndsWith(".prompt")
                                ? s.Remove(s.LastIndexOf(".prompt", StringComparison.Ordinal))
                                : s
                    )
            );

            builder.AppendLine($"{text.ToRepr()} ({token.StartIndex}, {token.EndIndex})");
            builder.AppendLine($"  └─ {scopeStr}");
        }

        return builder.ToString();
    }

    public static Prompt FromRawText(string text, ITokenizerProvider tokenizer)
    {
        using var _ = new CodeTimer();

        var result = tokenizer.TokenizeLine(text);

        return new Prompt { RawText = text, TokenizeResult = result };
    }
}
