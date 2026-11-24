using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ChangelogDialog>]
public partial class ChangelogDialog : UserControlBase
{
    public ChangelogDialog()
    {
        InitializeComponent();

        // Inset the markdown content from the right edge so the overlay scrollbar
        // doesn't cover the rightmost text (same approach as the docs viewer's
        // DocumentationMarkdownViewer). The markdown viewer composes a non-templated
        // inner ScrollViewer as a direct visual child.
        if (
            this.FindControl<BetterMarkdownScrollViewer>("MarkdownViewer") is { } markdownViewer
            && markdownViewer.GetVisualChildren().OfType<ScrollViewer>().FirstOrDefault() is { } innerViewer
        )
        {
            innerViewer.Padding = new Thickness(0, 0, 18, 0);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
