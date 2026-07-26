using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.UITests;

[Collection("TempDir")]
public class DocumentationMarkdownViewerTests : TestBase
{
    private static string LongDocument(string title) =>
        $"# {title}\n\n"
        + string.Join("\n\n", Enumerable.Range(1, 120).Select(i => $"Paragraph {i} of {title}."));

    /// <summary>
    /// Pages swap the document inside a reused scroll viewer, so without an explicit reset the
    /// reader lands part-way down a page they have never seen.
    /// </summary>
    [AvaloniaFact]
    public void Markdown_ResetsScrollToTop_OnPageChange()
    {
        var viewer = new DocumentationMarkdownViewer { Markdown = LongDocument("First Page") };
        var window = new Window
        {
            Width = 600,
            Height = 400,
            Content = viewer,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Reader scrolls part-way down the first page.
        viewer.ScrollValue = new Vector(0, 500);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewer.ScrollValue.Y > 0, "Precondition: the viewer should be scrolled down.");

        // Following a ? button loads a different page into the same viewer.
        viewer.Markdown = LongDocument("Second Page");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, viewer.ScrollValue.Y);
    }

    /// <summary>
    /// The reset above runs synchronously on the content change while anchor scrolling is
    /// deferred to a layout pass, so a deep link to a heading must still win over it.
    /// </summary>
    [AvaloniaFact]
    public void ScrollToAnchor_StillWins_AfterContentChange()
    {
        var document =
            "# Top\n\n"
            + string.Join("\n\n", Enumerable.Range(1, 80).Select(i => $"Filler paragraph {i}."))
            + "\n\n## Deep Heading\n\nContent under the deep heading.";

        var viewer = new DocumentationMarkdownViewer();
        var window = new Window
        {
            Width = 600,
            Height = 400,
            Content = viewer,
        };

        window.Show();

        // Mirrors a ? deep link: content is set, then the anchor is requested.
        viewer.Markdown = document;
        Dispatcher.UIThread.RunJobs();
        viewer.ScrollToAnchor("deep-heading");

        Dispatcher.UIThread.RunJobs();

        Assert.True(
            viewer.ScrollValue.Y > 0,
            $"Anchor scroll should survive the page-change reset, got Y={viewer.ScrollValue.Y}."
        );
    }
}
