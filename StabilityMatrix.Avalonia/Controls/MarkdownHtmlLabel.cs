using System;
using Avalonia;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// The HTML label behind <see cref="MarkdownViewer"/>. It fills the width it is offered, or with
/// <see cref="FitToContent"/> takes only the width its text needs, wrapping at the width offered.
/// </summary>
public class MarkdownHtmlLabel : HtmlLabel
{
    public static readonly StyledProperty<bool> FitToContentProperty = AvaloniaProperty.Register<
        MarkdownHtmlLabel,
        bool
    >(nameof(FitToContent));

    static MarkdownHtmlLabel()
    {
        AffectsMeasure<MarkdownHtmlLabel>(FitToContentProperty);
    }

    /// <summary>
    /// Size the width to the text, up to the width offered, instead of filling it.
    /// </summary>
    public bool FitToContent
    {
        get => GetValue(FitToContentProperty);
        set => SetValue(FitToContentProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (!FitToContent || double.IsPositiveInfinity(constraint.Width))
            return base.MeasureOverride(constraint);

        var horizontal = Padding.Left + Padding.Right + BorderThickness.Left + BorderThickness.Right;
        var vertical = Padding.Top + Padding.Bottom + BorderThickness.Top + BorderThickness.Bottom;
        var available = Math.Max(constraint.Width - horizontal, 0);

        // Lay out at the text's natural width, and wrap at the offered width only if it is wider
        _htmlContainer.MaxSize = default;
        _htmlContainer.PerformLayout();
        if (_htmlContainer.ActualSize.Width > available)
        {
            _htmlContainer.MaxSize = new Size(available, 0);
            _htmlContainer.PerformLayout();
        }

        // Rounded up so layout rounding can't hand back less than the text was laid out in
        var size = _htmlContainer.ActualSize;
        return new Size(Math.Ceiling(size.Width) + horizontal, Math.Ceiling(size.Height) + vertical);
    }
}
