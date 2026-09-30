using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Displays release notes with space between the document and the vertical scrollbar.
/// </summary>
public class ReleaseNotesMarkdownViewer : BetterMarkdownScrollViewer
{
    public ReleaseNotesMarkdownViewer()
    {
        if (
            this.GetVisualChildren().OfType<ScrollViewer>().FirstOrDefault() is
            { Content: Control content } viewer
        )
        {
            viewer.Content = null;
            viewer.Content = new Border { Child = content, Padding = new Thickness(0, 0, 18, 0) };
        }
    }
}
