using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;

namespace StabilityMatrix.UITests;

[Collection("TempDir")]
public class ReleaseNotesLayoutTests
{
    private static string StressNotes =>
        "## Changed\n\n"
        + string.Join(
            " ",
            Enumerable.Repeat("Long release notes must remain readable beside the scrollbar.", 40)
        )
        + "\n\n## Fixed\n\n- **Windows ROCm** packages now come from AMD's permanent repositories, "
        + string.Join(
            " ",
            Enumerable.Repeat(
                "including `inline code` and [linked release notes](https://example.org/releases).",
                25
            )
        )
        + "\n  - Nested lists also need to wrap without covering the scrollbar.\n\n"
        + "```python\nprint('"
        + new string('x', 300)
        + "')\n```\n\n"
        + string.Join("\n\n", Enumerable.Repeat("A final paragraph after the wide code block.", 20));

    [AvaloniaFact]
    public void ReleaseNotes_FitBesideScrollbar_AfterResizeAndScale()
    {
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            var viewer = new ReleaseNotesMarkdownViewer { Markdown = StressNotes };
            var window = new Window
            {
                Width = 700,
                Height = 400,
                Content = new LayoutTransformControl
                {
                    Child = viewer,
                    LayoutTransform = new ScaleTransform(scale, scale),
                },
            };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                foreach (var width in new[] { 700, 520, 400, 700 })
                {
                    window.Width = width;
                    Dispatcher.UIThread.RunJobs();
                    AssertDocumentFits(viewer);
                    var scroll = InnerScrollViewer(viewer);
                    scroll.Offset = new Vector(0, scroll.Extent.Height);
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(scroll.Offset.Y > 0);
                    AssertDocumentFits(viewer);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task UpdateDialog_PreloadedNotes_FitBesideScrollbar()
    {
        var model = TestBase.Services.GetRequiredService<UpdateViewModel>();
        model.ShowProgressBar = false;
        model.ReleaseNotes = StressNotes;
        var dialog = new UpdateDialog { DataContext = model };
        var window = new Window
        {
            Width = 700,
            Height = 700,
            Content = new Grid(),
        };
        var host = new BetterContentDialog
        {
            Content = dialog,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsFooterVisible = false,
        };
        try
        {
            window.Show();
            var showTask = host.ShowAsync(window);
            Dispatcher.UIThread.RunJobs();
            var viewer = dialog.GetVisualDescendants().OfType<BetterMarkdownScrollViewer>().Single();
            foreach (var width in new[] { 700, 632, 520 })
            {
                window.Width = width;
                Dispatcher.UIThread.RunJobs();
                Assert.True(viewer.Bounds.Height > 50);
                AssertDocumentFits(viewer);
            }

            var scroll = InnerScrollViewer(viewer);
            scroll.Offset = new Vector(0, 1400);
            Dispatcher.UIThread.RunJobs();
            SaveQaScreenshot(window);
            host.Hide();
            Dispatcher.UIThread.RunJobs();
            await showTask;
        }
        finally
        {
            window.Close();
        }
    }

    private static ScrollViewer InnerScrollViewer(BetterMarkdownScrollViewer viewer) =>
        viewer.GetVisualChildren().OfType<ScrollViewer>().Single();

    private static void AssertDocumentFits(BetterMarkdownScrollViewer viewer)
    {
        var scroll = InnerScrollViewer(viewer);
        var bar = scroll
            .GetVisualDescendants()
            .OfType<ScrollBar>()
            .Last(b => b.Orientation == Orientation.Vertical);
        Assert.True(bar.IsVisible);
        var contentRight = bar.TranslatePoint(new Point(), scroll)!.Value.X;
        var texts = viewer.GetVisualDescendants().OfType<CTextBlock>().ToArray();
        Assert.NotEmpty(texts);
        foreach (var text in texts)
        {
            var position = text.TranslatePoint(new Point(), scroll)!.Value;
            Assert.True(text.Bounds.Height > 0);
            Assert.True(
                position.X + text.Bounds.Width <= contentRight + 1,
                $"Text right={position.X + text.Bounds.Width}, scrollbar left={contentRight}, viewer width={viewer.Bounds.Width}"
            );
        }
    }

    private static void SaveQaScreenshot(Window window)
    {
        if (Environment.GetEnvironmentVariable("SM_RELEASE_NOTES_QA_DIR") is not { Length: > 0 } directory)
            return;

        Directory.CreateDirectory(directory);
        using var bitmap = new RenderTargetBitmap(
            new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height)
        );
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, "release-notes-dialog.png"));
    }
}
