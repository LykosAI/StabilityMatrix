using System;
using System.Windows.Input;
using Avalonia;
using Markdown.Avalonia;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// A <see cref="BetterMarkdownScrollViewer"/> that routes hyperlink clicks through a
/// bindable <see cref="LinkCommand"/> (so relative <c>.md</c> links can navigate in-app
/// and external links can open in the browser) and resolves relative image paths against
/// <see cref="ImageBaseUrl"/> via the engine's asset path root.
/// </summary>
public class DocumentationMarkdownViewer : BetterMarkdownScrollViewer
{
    /// <summary>
    /// Command invoked when a hyperlink is clicked. The command parameter is the raw href string.
    /// </summary>
    public static readonly StyledProperty<ICommand?> LinkCommandProperty = AvaloniaProperty.Register<
        DocumentationMarkdownViewer,
        ICommand?
    >(nameof(LinkCommand));

    /// <summary>
    /// Base URL used to resolve relative image paths in the rendered markdown
    /// (e.g. the raw URL of the current page's folder).
    /// </summary>
    public static readonly StyledProperty<string?> ImageBaseUrlProperty = AvaloniaProperty.Register<
        DocumentationMarkdownViewer,
        string?
    >(nameof(ImageBaseUrl));

    public ICommand? LinkCommand
    {
        get => GetValue(LinkCommandProperty);
        set => SetValue(LinkCommandProperty, value);
    }

    public string? ImageBaseUrl
    {
        get => GetValue(ImageBaseUrlProperty);
        set => SetValue(ImageBaseUrlProperty, value);
    }

    public DocumentationMarkdownViewer()
    {
        ApplyLinkCommand();
        ApplyImageBaseUrl();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LinkCommandProperty)
        {
            ApplyLinkCommand();
        }
        else if (change.Property == ImageBaseUrlProperty)
        {
            ApplyImageBaseUrl();
        }
    }

    private void ApplyLinkCommand()
    {
        // The engine (IMarkdownEngine) owns the HyperlinkCommand used for all rendered links.
        if (Engine is IMarkdownEngine engine)
        {
            engine.HyperlinkCommand = LinkCommand;
        }
    }

    private void ApplyImageBaseUrl()
    {
        // AssetPathRoot flows through to the engine's bitmap loader so relative image
        // paths resolve against the raw docs URL.
        AssetPathRoot = ImageBaseUrl ?? string.Empty;
    }
}
