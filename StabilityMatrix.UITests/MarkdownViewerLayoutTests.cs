using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StabilityMatrix.Avalonia.Controls;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace StabilityMatrix.UITests;

[Collection("TempDir")]
public class MarkdownViewerLayoutTests
{
    private static readonly FieldInfo HtmlContainerField = typeof(HtmlControl).GetField(
        "_htmlContainer",
        BindingFlags.Instance | BindingFlags.NonPublic
    )!;

    private static readonly string LongText = string.Join(
        " ",
        Enumerable.Repeat("Keep his face shape, snout and ear shape identical.", 12)
    );

    [AvaloniaFact]
    public void LongText_InHeightUnboundedHost_WrapsWithinTheControl()
    {
        foreach (var fitToContent in new[] { false, true })
        {
            var (label, container) = LayOutInBubble(LongText, fitToContent);

            Assert.True(container.ActualSize.Height > 100, "text should wrap onto several lines");
            Assert.True(
                container.ActualSize.Width <= label.Bounds.Width + 0.5,
                $"text laid out {container.ActualSize.Width} wide in a {label.Bounds.Width} wide control"
            );
            Assert.True(label.Bounds.Width <= 520, $"control is {label.Bounds.Width} wide, offered 520");
        }
    }

    [AvaloniaFact]
    public void ShortText_FitToContent_TakesOnlyTheWidthItNeeds()
    {
        var (fitted, fittedContainer) = LayOutInBubble("hi!", fitToContent: true);
        var (filled, _) = LayOutInBubble("hi!", fitToContent: false);

        Assert.True(fitted.DesiredSize.Width < 100, $"fitted control wants {fitted.DesiredSize.Width}");
        Assert.True(fittedContainer.ActualSize.Width <= fitted.Bounds.Width + 0.5);
        Assert.Equal(520, filled.DesiredSize.Width, 0.5);
    }

    /// <summary>
    /// Lays the text out in a host shaped like an Image Lab chat bubble: an Auto grid column offers
    /// unbounded width, the bubble's MaxWidth bounds it, and the StackPanel offers unbounded height.
    /// </summary>
    private static (HtmlControl Label, HtmlContainer Container) LayOutInBubble(string text, bool fitToContent)
    {
        var viewer = new MarkdownViewer { Text = text, FitToContent = fitToContent };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new ScrollViewer
            {
                Content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto"),
                    Children =
                    {
                        new Border
                        {
                            MaxWidth = 550,
                            Padding = new Thickness(14, 10),
                            Child = new StackPanel { MaxWidth = 520, Children = { viewer } },
                        },
                    },
                },
            },
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var label = viewer.GetVisualDescendants().OfType<HtmlControl>().Single();
            return (label, (HtmlContainer)HtmlContainerField.GetValue(label)!);
        }
        finally
        {
            window.Close();
        }
    }
}
