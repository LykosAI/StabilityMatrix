using Markdown.Avalonia;
using StabilityMatrix.Avalonia.Styles.Markdown;

namespace StabilityMatrix.Avalonia.Controls;

public class BetterMarkdownScrollViewer : MarkdownScrollViewer
{
    public BetterMarkdownScrollViewer()
    {
        MarkdownStyleName = "Empty";
        MarkdownStyle = new MarkdownStyleFluentAvalonia();
    }
}
