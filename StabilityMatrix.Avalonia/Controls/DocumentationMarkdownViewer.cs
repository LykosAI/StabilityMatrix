using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Markdown.Avalonia;
using StabilityMatrix.Core.Models.Documentation;

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
        // The engine owns the HyperlinkCommand used for all rendered links. The Engine getter
        // always returns an IMarkdownEngine2 (custom IMarkdownEngine values are upgraded to a
        // wrapper that only implements IMarkdownEngine2), so match on that interface.
        if (Engine is IMarkdownEngine2 engine)
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

    private static readonly string[] HeadingClasses =
    [
        "Heading1",
        "Heading2",
        "Heading3",
        "Heading4",
        "Heading5",
        "Heading6",
    ];

    /// <summary>
    /// Scrolls the rendered content so the heading matching the given GitHub-style anchor slug
    /// is brought to the top of the viewport.
    /// </summary>
    /// <param name="anchor">The bare heading slug (no leading <c>#</c>).</param>
    /// <returns><c>true</c> if a matching heading was found at call time; otherwise <c>false</c>.</returns>
    public bool ScrollToAnchor(string anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
            return false;

        var slug = DocumentationPathResolver.Slugify(anchor);
        if (slug.Length == 0)
            return false;

        // Content is built synchronously when Markdown changes, but layout/measure (needed for
        // TranslatePoint) only runs on the next layout pass — defer the actual scroll.
        var found = FindHeadingBySlug(slug) is not null;

        Dispatcher.UIThread.Post(
            () =>
            {
                var target = FindHeadingBySlug(slug);
                if (target is not null)
                    ScrollHeadingIntoView(target);
            },
            DispatcherPriority.Background
        );

        return found;
    }

    /// <summary>
    /// Locates the heading control whose slug matches, applying GitHub-style duplicate suffixes
    /// (<c>-1</c>, <c>-2</c>, ...) in document order.
    /// </summary>
    private CTextBlock? FindHeadingBySlug(string slug)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var descendant in this.GetVisualDescendants())
        {
            if (descendant is not CTextBlock textBlock || !IsHeading(textBlock))
                continue;

            var baseSlug = DocumentationPathResolver.Slugify(textBlock.Text ?? string.Empty);
            if (baseSlug.Length == 0)
                continue;

            string effectiveSlug;
            if (seen.TryGetValue(baseSlug, out var count))
            {
                effectiveSlug = $"{baseSlug}-{count}";
                seen[baseSlug] = count + 1;
            }
            else
            {
                effectiveSlug = baseSlug;
                seen[baseSlug] = 1;
            }

            if (string.Equals(effectiveSlug, slug, StringComparison.Ordinal))
                return textBlock;
        }

        return null;
    }

    private static bool IsHeading(StyledElement control)
    {
        foreach (var cls in HeadingClasses)
        {
            if (control.Classes.Contains(cls))
                return true;
        }

        return false;
    }

    private void ScrollHeadingIntoView(Visual heading)
    {
        // Position of the heading relative to this control's viewport, plus the current scroll
        // offset, gives the heading's Y within the scrollable content.
        var current = ScrollValue;
        var point = heading.TranslatePoint(new Point(0, 0), this);
        if (point is null)
            return;

        var targetY = Math.Max(0, point.Value.Y + current.Y);
        ScrollValue = new Vector(current.X, targetY);
    }
}
