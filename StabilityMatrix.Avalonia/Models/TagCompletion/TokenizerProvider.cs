using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Avalonia.Extensions;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public class TokenizerProvider : ITokenizerProvider
{
    private readonly Registry registry = new(new RegistryOptions(ThemeName.DarkPlus));
    private IGrammar grammar;
    
    public TokenizerProvider()
    {
        SetPromptGrammar();
    }
    
    /// <inheritdoc />
    public ITokenizeLineResult TokenizeLine(string lineText)
    {
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
