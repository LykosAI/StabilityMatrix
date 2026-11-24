using System;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// JSON syntax highlighting tuned for the app's dark surfaces. AvaloniaEdit's built-in
/// definitions use light-theme colours whose digits are nearly invisible on a dark background.
/// </summary>
public static class JsonHighlighting
{
    private static readonly Lazy<IHighlightingDefinition> DarkLazy = new(LoadDark);

    public static IHighlightingDefinition Dark => DarkLazy.Value;

    private static IHighlightingDefinition LoadDark()
    {
        const string xshd = """
            <SyntaxDefinition name="JsonDark" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                <Color name="Key" foreground="#9CDCFE" />
                <Color name="String" foreground="#CE9178" />
                <Color name="Number" foreground="#B5CEA8" />
                <Color name="Keyword" foreground="#569CD6" fontWeight="bold" />
                <RuleSet ignoreCase="false">
                    <Rule color="Key">"(?:[^"\\]|\\.)*"(?=\s*:)</Rule>
                    <Span color="String" begin="&quot;" end="&quot;">
                        <RuleSet>
                            <Span begin="\\" end="." />
                        </RuleSet>
                    </Span>
                    <Rule color="Number">-?\b\d+(\.\d+)?([eE][+-]?\d+)?\b</Rule>
                    <Keywords color="Keyword">
                        <Word>true</Word>
                        <Word>false</Word>
                        <Word>null</Word>
                    </Keywords>
                </RuleSet>
            </SyntaxDefinition>
            """;

        using var reader = XmlReader.Create(new System.IO.StringReader(xshd));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
