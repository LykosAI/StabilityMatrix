using Markdown.Avalonia;
using StabilityMatrix.Avalonia.Styles.Markdown;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Fix MarkdownScrollViewer IBrush errors and not working with Avalonia 11.2.0
/// </summary>
public class BetterMarkdownScrollViewer : MarkdownScrollViewer
{
    public BetterMarkdownScrollViewer()
    {
        MarkdownStyleName = "Empty";
        MarkdownStyle = new MarkdownStyleFluentAvalonia();
    }
}
