using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Tokens;
using StabilityMatrix.Core.Services;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Models.Inference;

public record Prompt
{
    public required string RawText { get; init; }

    public required ITokenizeLineResult TokenizeResult { get; init; }

    public required ITokenizerProvider Tokenizer { get; init; }

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
    public void Process(bool processWildcards = true)
    {
        if (IsProcessed)
            return;

        var (promptExtraNetworks, processedText) = GetExtraNetworks(processWildcards: processWildcards);
        ExtraNetworks = promptExtraNetworks;
        ProcessedText = processedText;
    }

    /// <summary>
    /// Verifies that extra network files exists locally.
    /// </summary>
    /// <exception cref="PromptValidationError">Thrown if a filename does not exist</exception>
    public void ValidateExtraNetworks(IModelIndexService indexService)
    {
        GetExtraNetworks(indexService, false);
    }

    /// <summary>
    /// Get ExtraNetworks as local model files and weights.
    /// </summary>
    public IEnumerable<(
        LocalModelFile ModelFile,
        double? ModelWeight,
        double? ClipWeight
    )> GetExtraNetworksAsLocalModels(IModelIndexService indexService)
    {
        if (ExtraNetworks is null)
        {
            throw new InvalidOperationException(
                "Prompt must be processed before calling GetExtraNetworksAsLocalModels"
            );
        }

        foreach (var network in ExtraNetworks)
        {
            var sharedFolderType = network.Type.ConvertTo<SharedFolderType>();

            if (!indexService.ModelIndex.TryGetValue(sharedFolderType, out var modelList))
            {
                throw new ApplicationException($"Model {network.Name} does not exist in index");
            }

            var localModel = modelList.FirstOrDefault(m => m.FileNameWithoutExtension == network.Name);
            if (localModel == null)
            {
                throw new ApplicationException($"Model {network.Name} does not exist in index");
            }

            yield return (localModel, network.ModelWeight, network.ClipWeight);
        }
    }

    private int GetSafeEndIndex(int index)
    {
        return Math.Min(index, RawText.Length);
    }

    private (List<PromptExtraNetwork> promptExtraNetworks, string processedText) GetExtraNetworks(
        IModelIndexService? indexService = null,
        bool processWildcards = true
    )
    {
        // Parse tokens "meta.structure.network.prompt"
        // "<": "punctuation.definition.network.begin.prompt"
        // (type): "meta.embedded.network.type.prompt"
        // ":": "punctuation.separator.variable.prompt"
        // (content): "meta.embedded.network.model.prompt"
        // ">": "punctuation.definition.network.end.prompt"
        using var tokens = TokenizeResult.Tokens.Cast<IToken>().GetEnumerator();

        // Maintain both token and text stacks for validation
        var outputTokens = new Stack<IToken>();
        var outputText = new Stack<string>();
        var wildcardStack = new Stack<StringBuilder>();

        // Store extra networks
        var promptExtraNetworks = new List<PromptExtraNetwork>();

        while (tokens.MoveNext())
        {
            var currentToken = tokens.Current;

            // For any invalid syntax, throw
            if (currentToken.Scopes.Any(s => s.Contains("invalid.illegal")))
            {
                // Generic
                throw new PromptSyntaxError(
                    "Invalid Token",
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }

            // Comments - ignore
            if (currentToken.Scopes.Any(s => s.Contains("comment.line")))
            {
                continue;
            }

            // Handle wildcard start
            if (
                processWildcards
                && currentToken.Scopes.Contains("punctuation.definition.wildcard.begin.prompt")
            )
            {
                wildcardStack.Push(new StringBuilder());
                continue;
            }

            // Handle wildcard end
            if (
                processWildcards && currentToken.Scopes.Contains("punctuation.definition.wildcard.end.prompt")
            )
            {
                if (wildcardStack.Count == 0)
                    continue;

                var wildcardContent = wildcardStack.Pop();
                var options = wildcardContent
                    .ToString()
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .ToList();

                if (options.Count == 0)
                {
                    outputTokens.Push(currentToken);
                    outputText.Push(string.Empty);
                    continue;
                }

                // Process wildcard selection
                var selectedIndex = RandomNumberGenerator.GetInt32(options.Count);
                var selectedOption = options[selectedIndex];

                // Process selected option
                var tempPrompt = FromRawText(selectedOption, Tokenizer);
                tempPrompt.Process();

                // Collect networks from selected option
                if (tempPrompt.ExtraNetworks is { Count: > 0 } networks)
                {
                    promptExtraNetworks.AddRange(networks);
                }

                outputTokens.Push(currentToken);
                outputText.Push(tempPrompt.ProcessedText ?? string.Empty);
                continue;
            }

            // Handle wildcard content
            if (processWildcards && wildcardStack.Count > 0)
            {
                var currentWildcard = wildcardStack.Peek();
                currentWildcard.Append(
                    RawText[currentToken.StartIndex..GetSafeEndIndex(currentToken.EndIndex)]
                );
                continue;
            }

            // Find start of network token, until then just add to output
            if (!currentToken.Scopes.Contains("punctuation.definition.network.begin.prompt"))
            {
                // Normal tags - Push to output
                outputTokens.Push(currentToken);
                outputText.Push(RawText[currentToken.StartIndex..GetSafeEndIndex(currentToken.EndIndex)]);
                continue;
            }

            // Expect next token to be network type
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }
            currentToken = tokens.Current;

            if (!currentToken.Scopes.Contains("meta.embedded.network.type.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedType(
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }

            var networkType = RawText[currentToken.StartIndex..GetSafeEndIndex(currentToken.EndIndex)];

            // Match network type
            var parsedNetworkType = networkType switch
            {
                "lora" => PromptExtraNetworkType.Lora,
                "lyco" => PromptExtraNetworkType.LyCORIS,
                "embedding" => PromptExtraNetworkType.Embedding,
                _
                    => throw PromptValidationError.Network_UnknownType(
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    )
            };

            // Skip colon token
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }
            currentToken = tokens.Current;

            // Ensure next token is colon
            if (!currentToken.Scopes.Contains("punctuation.separator.variable.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedSeparator(
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }

            // Get model name
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }
            currentToken = tokens.Current;

            if (!currentToken.Scopes.Contains("meta.embedded.network.model.prompt"))
            {
                throw PromptSyntaxError.Network_ExpectedName(
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }

            var modelName = RawText[currentToken.StartIndex..GetSafeEndIndex(currentToken.EndIndex)];

            // If index service provided, validate model name
            if (indexService != null)
            {
                var localModelList = indexService.ModelIndex.GetOrAdd(
                    parsedNetworkType.ConvertTo<SharedFolderType>()
                );
                var localModel = localModelList.FirstOrDefault(
                    m => Path.GetFileNameWithoutExtension(m.FileName) == modelName
                );
                if (localModel == null)
                {
                    throw PromptValidationError.Network_UnknownModel(
                        modelName,
                        parsedNetworkType,
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    );
                }
            }

            // Skip another colon token
            if (!tokens.MoveNext())
            {
                throw PromptSyntaxError.UnexpectedEndOfText(
                    currentToken.StartIndex,
                    GetSafeEndIndex(currentToken.EndIndex)
                );
            }
            currentToken = tokens.Current;

            double? weight = null;
            // If its a ending token instead, we can end here, otherwise keep parsing for weight
            if (!currentToken.Scopes.Contains("punctuation.definition.network.end.prompt"))
            {
                // Ensure next token is colon
                if (!currentToken.Scopes.Contains("punctuation.separator.variable.prompt"))
                {
                    throw PromptSyntaxError.Network_ExpectedSeparator(
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    );
                }

                // Get model weight
                if (!tokens.MoveNext())
                {
                    throw PromptSyntaxError.UnexpectedEndOfText(
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    );
                }
                currentToken = tokens.Current;

                if (!currentToken.Scopes.Contains("constant.numeric"))
                {
                    throw PromptSyntaxError.Network_ExpectedWeight(
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    );
                }

                var modelWeight = RawText[currentToken.StartIndex..GetSafeEndIndex(currentToken.EndIndex)];

                // Convert to double
                if (!double.TryParse(modelWeight, CultureInfo.InvariantCulture, out var weightValue))
                {
                    throw PromptValidationError.Network_InvalidWeight(
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    );
                }
                weight = weightValue;

                // Expect end
                if (!tokens.MoveNext())
                {
                    throw PromptSyntaxError.UnexpectedEndOfText(
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    );
                }

                currentToken = tokens.Current;

                if (!currentToken.Scopes.Contains("punctuation.definition.network.end.prompt"))
                {
                    throw PromptSyntaxError.UnexpectedEndOfText(
                        currentToken.StartIndex,
                        GetSafeEndIndex(currentToken.EndIndex)
                    );
                }
            }

            // Modified embedding handling with stack validation
            if (parsedNetworkType is PromptExtraNetworkType.Embedding)
            {
                outputTokens.Push(currentToken);
                outputText.Push(
                    weight is null ? $"embedding:{modelName}" : $"(embedding:{modelName}:{weight:F2})"
                );
            }
            else
            {
                // Original colon cleanup logic
                if (
                    outputTokens.TryPeek(out var lastToken)
                    && lastToken.Scopes.Contains("punctuation.separator.variable.prompt")
                )
                {
                    outputTokens.Pop();
                    outputText.Pop();
                }

                promptExtraNetworks.Add(
                    new PromptExtraNetwork
                    {
                        Type = parsedNetworkType,
                        Name = modelName,
                        ModelWeight = weight
                    }
                );
            }
        }

        // Build final text maintaining original order
        var processedText = string.Concat(outputText.Reverse());
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
                token
                    .Scopes.Where(s => s != "source.prompt")
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
        using var _ = CodeTimer.StartDebug();

        var result = tokenizer.TokenizeLine(text);

        return new Prompt
        {
            RawText = text,
            TokenizeResult = result,
            Tokenizer = tokenizer
        };
    }
}
