using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
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
    public IImage? Image { get; set; }

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
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
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
