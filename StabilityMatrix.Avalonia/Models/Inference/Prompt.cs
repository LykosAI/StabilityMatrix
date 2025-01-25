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

    /// <summary>
    /// Verifies that extra network files exists locally.
    /// </summary>
    /// <exception cref="PromptValidationError">Thrown if a filename does not exist</exception>
    public void ValidateExtraNetworks(IModelIndexService indexService)
    {
        GetExtraNetworks(indexService);
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
        IModelIndexService? indexService = null
    )
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

            // For embeddings, we add to the prompt, and not to the extra networks list
            if (parsedNetworkType is PromptExtraNetworkType.Embedding)
            {
                // Push to output in Comfy format
                // <embedding:model> -> embedding:model
                // <embedding:model:weight> -> (embedding:model:weight)
                outputTokens.Push(currentToken);

                outputText.Push(
                    weight is null ? $"embedding:{modelName}" : $"(embedding:{modelName}:{weight:F2})"
                );
            }
            // Cleanups for separate extra networks
            else
            {
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
        }

        var processedText = string.Join("", outputText.Reverse());

        // Process wildcards after other tokens are handled
        processedText = ProcessWildcards(processedText);

        return (promptExtraNetworks, processedText);
    }

    /// <summary>
    /// Processes wildcard patterns in the format {option1|option2|option3} and randomly selects one option
    /// </summary>
    private string ProcessWildcards(string input)
    {
        // Pre-check for performance
        if (!input.Contains('{'))
        {
            return input;
        }

        // First validate that all braces are properly closed
        var braceCount = 0;
        var lastOpenBraceIndex = -1;

        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '{')
            {
                braceCount++;
                lastOpenBraceIndex = i;
            }
            else if (input[i] == '}')
            {
                braceCount--;
                if (braceCount < 0)
                {
                    throw new PromptValidationError("Unexpected closing brace", i, i + 1);
                }
            }
        }

        if (braceCount > 0)
        {
            throw new PromptValidationError(
                "Unclosed brace in wildcard",
                lastOpenBraceIndex,
                lastOpenBraceIndex + 1
            );
        }

        // Use precompiled regex for better performance
        const string pattern = @"\{(?:[^{}]|(?<Open>\{)|(?<Close-Open>\}))+(?(Open)(?!))\}";
        var regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        return regex.Replace(
            input,
            match =>
            {
                var options = match.Value.Trim('{', '}');
                var splitOptions = SplitPreservingNested(options, '|');

                // More robust whitespace handling
                var trimmedOptions = splitOptions
                    .Select(opt => opt.Trim())
                    .Where(opt => !string.IsNullOrEmpty(opt))
                    .ToArray();

                if (trimmedOptions.Length == 0)
                {
                    throw new PromptValidationError(
                        "No valid options in wildcard choice",
                        match.Index,
                        match.Index + match.Length
                    );
                }

                var randomIndex = RandomNumberGenerator.GetInt32(trimmedOptions.Length);
                return trimmedOptions[randomIndex];
            }
        );
    }

    /// <summary>
    /// Splits a string by a delimiter while preserving nested structures
    /// </summary>
    private static string[] SplitPreservingNested(string input, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder(input.Length);
        var nestLevel = 0;

        foreach (var c in input)
        {
            if (c == '{')
                nestLevel++;
            else if (c == '}')
                nestLevel--;

            if (c == delimiter && nestLevel == 0)
            {
                AddCurrent();
                continue;
            }

            current.Append(c);
        }

        AddCurrent();
        return result.ToArray();

        void AddCurrent()
        {
            if (current.Length <= 0)
                return;
            result.Add(current.ToString());
            current.Clear();
        }
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

        return new Prompt { RawText = text, TokenizeResult = result };
    }
}
