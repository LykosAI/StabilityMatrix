using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Avalonia.Extensions;
using Injectio.Attributes;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

[RegisterSingleton<ITokenizerProvider, TokenizerProvider>]
public class TokenizerProvider : ITokenizerProvider
{
    private readonly Registry registry = new(new RegistryOptions(ThemeName.DarkPlus));
    private IGrammar? grammar;

    /// <inheritdoc />
    public ITokenizeLineResult TokenizeLine(string lineText)
    {
        if (grammar is null)
        {
            SetPromptGrammar();
        }
        return grammar.TokenizeLine(lineText);
    }

    [MemberNotNull(nameof(grammar))]
    public void SetPromptGrammar()
    {
        using var stream = Assets.ImagePromptLanguageJson.Open();
        grammar = registry.LoadGrammarFromStream(stream);
    }

    public void SetGrammar(string scopeName)
    {
        grammar = registry.LoadGrammar(scopeName);
    }
}
