using System;
using Avalonia.Controls.Documents;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Styles;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Controls.CodeCompletion;

/// <summary>
/// Provides entries in AvaloniaEdit completion window.
/// </summary>
public class CompletionData : ICompletionData
{
    /// <inheritdoc />
    public string Text { get; }

    /// <inheritdoc />
    public string? Description { get; init; }

    /// <inheritdoc />
    public ImageSource? ImageSource { get; set; }

    /// <summary>
    /// Title of the image.
    /// </summary>
    public string? ImageTitle { get; set; }

    /// <summary>
    /// Subtitle of the image.
    /// </summary>
    public string? ImageSubtitle { get; set; }

    /// <inheritdoc />
    public IconData? Icon { get; init; }

    private InlineCollection? _textInlines;

    /// <summary>
    /// Get the current inlines
    /// </summary>
    public InlineCollection TextInlines => _textInlines ??= CreateInlines();

    /// <inheritdoc />
    public double Priority { get; init; }

    public CompletionData(string text)
    {
        Text = text;
    }

    /// <summary>
    /// Create text block inline runs from text.
    /// </summary>
    private InlineCollection CreateInlines()
    {
        // Create a span for each character in the text.
        var chars = Text.ToCharArray();
        var inlines = new InlineCollection();

        foreach (var c in chars)
        {
            var run = new Run(c.ToString());
            inlines.Add(run);
        }

        return inlines;
    }

    /// <inheritdoc />
    public void Complete(
        TextArea textArea,
        ISegment completionSegment,
        InsertionRequestEventArgs eventArgs,
        Func<ICompletionData, string>? prepareText = null
    )
    {
        var text = Text;

        if (prepareText is not null)
        {
            text = prepareText(this);
        }

        // Capture initial offset before replacing text, since it will change
        var initialOffset = completionSegment.Offset;

        // Replace text
        textArea.Document.Replace(completionSegment, text);

        // Append text if requested
        if (eventArgs.AppendText is { } appendText && !string.IsNullOrEmpty(appendText))
        {
            var end = initialOffset + text.Length;
            textArea.Document.Insert(end, appendText);
            textArea.Caret.Offset = end + appendText.Length;
        }
    }

    /// <inheritdoc />
    public void UpdateCharHighlighting(string searchText)
    {
        if (TextInlines is null)
        {
            throw new NullReferenceException("TextContent is null");
        }

        var defaultColor = ThemeColors.CompletionForegroundBrush;
        var highlightColor = ThemeColors.CompletionSelectionForegroundBrush;

        // Match characters in the text with the search text from the start
        foreach (var (i, currentChar) in Text.Enumerate())
        {
            var inline = TextInlines[i];

            // If longer than text, set to default color
            if (i >= searchText.Length)
            {
                inline.Foreground = defaultColor;
                continue;
            }

            // If char matches, highlight it
            if (currentChar == searchText[i])
            {
                inline.Foreground = highlightColor;
            }
            // For mismatch, set to default color
            else
            {
                inline.Foreground = defaultColor;
            }
        }
    }

    /// <inheritdoc />
    public void ResetCharHighlighting()
    {
        // TODO: handle light theme foreground variant
        var defaultColor = ThemeColors.CompletionForegroundBrush;

        foreach (var inline in TextInlines)
        {
            inline.Foreground = defaultColor;
        }
    }
}
