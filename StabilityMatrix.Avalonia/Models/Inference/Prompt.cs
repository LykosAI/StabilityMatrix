using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StabilityMatrix.Avalonia.Models.Inference.Tokens;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Models.Inference;

public record Prompt
{
    public required string RawText { get; init; }
    
    public required ITokenizeLineResult TokenizeResult { get; init; }

    private List<PromptExtraNetwork>? extraNetworks;
    
    public IReadOnlyList<PromptExtraNetwork> ExtraNetworks => extraNetworks ??= GetExtraNetworks();
    
    /// <summary>
    /// Returns processed text suitable for sending to inference backend.
    /// This excludes extra network (i.e. LORA) tokens.
    /// </summary>
    private string GetProcessedText()
    {
        // TODO
        return RawText;
    }

    private List<PromptExtraNetwork> GetExtraNetworks()
    {
        // Parse tokens "meta.structure.network.prompt"
        // "<": "punctuation.definition.network.begin.prompt"
        // (type): "meta.embedded.network.type.prompt"
        // ":": "punctuation.separator.variable.prompt"
        // (content): "meta.embedded.network.model.prompt" 
        // ">": "punctuation.definition.network.end.prompt"
        using var tokens = TokenizeResult.Tokens.Cast<IToken>().GetEnumerator();

        // Store non-network tokens
        var output = new StringBuilder();
        
        // Store extra networks
        var promptExtraNetworks = new List<PromptExtraNetwork>();

        while (tokens.MoveNext())
        {
            var token = tokens.Current;

            // Find start of network token, until then just add to output
            if (!token.Scopes.Contains("punctuation.definition.network.begin.prompt"))
            {
                output.Append(RawText[token.StartIndex..token.EndIndex]);
                continue;
            }
            
            // Expect next token to be network type
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(token.StartIndex, token.EndIndex);
            }
            var networkTypeToken = tokens.Current;
            
            if (!networkTypeToken.Scopes.Contains("meta.embedded.network.type.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedType(
                    networkTypeToken.StartIndex, networkTypeToken.EndIndex);
            }
            
            var networkType = RawText[networkTypeToken.StartIndex..networkTypeToken.EndIndex];
            
            // Match network type
            var parsedNetworkType = networkType switch
            {
                "lora" => PromptExtraNetworkType.Lora,
                "lycoris" => PromptExtraNetworkType.LyCORIS,
                "embedding" => PromptExtraNetworkType.Embedding,
                _ => throw PromptValidationError.Network_UnknownType(
                    networkTypeToken.StartIndex, networkTypeToken.EndIndex)
            };
            
            // Skip colon token
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(token.StartIndex, token.EndIndex);
            }
            // Ensure next token is colon
            if (!tokens.Current.Scopes.Contains("punctuation.separator.variable.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedSeparator(
                    tokens.Current!.StartIndex, tokens.Current!.EndIndex);
            }
            
            // Get model name
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(token.StartIndex, token.EndIndex);
            }
            
            var modelNameToken = tokens.Current;
            if (!tokens.Current.Scopes.Contains("meta.embedded.network.model.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedName(
                    tokens.Current!.StartIndex, tokens.Current!.EndIndex);
            }
            
            var modelName = RawText[modelNameToken.StartIndex..modelNameToken.EndIndex];
            
            // Skip another colon token
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(token.StartIndex, token.EndIndex);
            }
            
            // If its a ending token instead, we can end here
            if (tokens.Current.Scopes.Contains("punctuation.definition.network.end.prompt"))
            {
                promptExtraNetworks.Add(new PromptExtraNetwork
                {
                    Type = parsedNetworkType,
                    Name = modelName
                });
                continue;
            }
            
            // Ensure next token is colon
            if (!tokens.Current.Scopes.Contains("punctuation.separator.variable.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedSeparator(
                    tokens.Current!.StartIndex, tokens.Current!.EndIndex);
            }
            
            // Get model weight
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(token.StartIndex, token.EndIndex);
            }
            
            var modelWeightToken = tokens.Current;
            if (!tokens.Current.Scopes.Contains("constant.numeric"))
            {
                throw PromptSyntaxError.Network_ExpectedWeight(
                    tokens.Current!.StartIndex, tokens.Current!.EndIndex);
            }
            
            var modelWeight = RawText[modelWeightToken.StartIndex..modelWeightToken.EndIndex];
            
            // Convert to double
            if (!double.TryParse(modelWeight, out var weight))
            {
                throw PromptValidationError.Network_InvalidWeight(
                    modelWeightToken.StartIndex, modelWeightToken.EndIndex);
            }
            
            // Expect end
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(token.StartIndex, token.EndIndex);
            }
            var endToken = tokens.Current;
            if (!endToken.Scopes.Contains("punctuation.definition.network.end.prompt"))
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    endToken.StartIndex, endToken.EndIndex);
            }
            
            // Add to output
            promptExtraNetworks.Add(new PromptExtraNetwork
            {
                Type = parsedNetworkType,
                Name = modelName,
                ModelWeight = weight
            });
        }

        return promptExtraNetworks;
    }

    public string GetDebugText()
    {
        var builder = new StringBuilder();

        foreach (var token in TokenizeResult.Tokens)
        {
            // Get token text
            var text = RawText[token.StartIndex..token.EndIndex];
            
            // Format scope
            var scopeStr = string.Join(", ", token.Scopes
                .Where(s => s != "source.prompt")
                .Select(s => s.EndsWith(".prompt") ? s.Remove(s.LastIndexOf(".prompt", StringComparison.Ordinal)) : s));
            
            builder.AppendLine($"{text.ToRepr()} ({token.StartIndex}, {token.EndIndex})");
            builder.AppendLine($"  └─ {scopeStr}");
        }

        return builder.ToString();
    }
    
    public static Prompt FromRawText(string text, ITokenizerProvider tokenizer)
    {
        using var _ = new CodeTimer();
        
        var result = tokenizer.TokenizeLine(text);

        return new Prompt
        {
            RawText = text,
            TokenizeResult = result
        };
    }
}
