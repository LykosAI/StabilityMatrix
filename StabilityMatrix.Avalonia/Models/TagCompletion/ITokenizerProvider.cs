using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public interface ITokenizerProvider
{
    /// <summary>
    /// Returns a <see cref="ITokenizeLineResult"/> for the given line.
    /// </summary>
    ITokenizeLineResult TokenizeLine(string lineText);
}
