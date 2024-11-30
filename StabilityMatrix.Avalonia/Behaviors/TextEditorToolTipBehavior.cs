using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using NLog;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Extensions;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Behaviors;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class TextEditorToolTipBehavior : Behavior<TextEditor>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private TextEditor textEditor = null!;

    /// <summary>
    /// The current ToolTip, if open.
    /// Is set to null when the Tooltip is closed.
    /// </summary>
    private ToolTip? toolTip;

    public static readonly StyledProperty<ITokenizerProvider?> TokenizerProviderProperty =
        AvaloniaProperty.Register<TextEditorCompletionBehavior, ITokenizerProvider?>("TokenizerProvider");

    public ITokenizerProvider? TokenizerProvider
    {
        get => GetValue(TokenizerProviderProperty);
        set => SetValue(TokenizerProviderProperty, value);
    }

    public static readonly StyledProperty<bool> IsEnabledProperty = AvaloniaProperty.Register<
        TextEditorCompletionBehavior,
        bool
    >("IsEnabled", true);

    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is not { } editor)
        {
            throw new NullReferenceException("AssociatedObject is null");
        }

        textEditor = editor;
        textEditor.PointerHover += TextEditor_OnPointerHover;
        textEditor.PointerHoverStopped += TextEditor_OnPointerHoverStopped;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        textEditor.PointerHover -= TextEditor_OnPointerHover;
        textEditor.PointerHoverStopped -= TextEditor_OnPointerHoverStopped;
    }

    /*private void OnVisualLinesChanged(object? sender, EventArgs e)
    {
        _toolTip?.Close(this);
    }*/

    private void TextEditor_OnPointerHoverStopped(object? sender, PointerEventArgs e)
    {
        if (!IsEnabled)
            return;

        if (sender is TextEditor editor)
        {
            ToolTip.SetIsOpen(editor, false);
            e.Handled = true;
        }
    }

    private void TextEditor_OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (!IsEnabled)
            return;

        TextViewPosition? position;

        var textArea = textEditor.TextArea;

        try
        {
            position = textArea.TextView.GetPositionFloor(
                e.GetPosition(textArea.TextView) + textArea.TextView.ScrollOffset
            );
        }
        catch (ArgumentOutOfRangeException)
        {
            // TODO: check why this happens
            e.Handled = true;
            return;
        }

        if (!position.HasValue || position.Value.Location.IsEmpty || position.Value.IsAtEndOfLine)
        {
            return;
        }

        /*var args = new ToolTipRequestEventArgs { InDocument = position.HasValue };

        args.LogicalPosition = position.Value.Location;
        args.Position = textEditor.Document.GetOffset(position.Value.Line, position.Value.Column);*/

        // Get the ToolTip data
        if (GetCaretToolTipData(position.Value) is not { } data)
        {
            return;
        }

        if (toolTip == null)
        {
            toolTip = new ToolTip { MaxWidth = 400 };

            ToolTip.SetShowDelay(textEditor, 0);
            ToolTip.SetPlacement(textEditor, PlacementMode.Pointer);
            ToolTip.SetTip(textEditor, toolTip);

            toolTip
                .GetPropertyChangedObservable(ToolTip.IsOpenProperty)
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(c =>
                {
                    if (c.NewValue as bool? != true)
                    {
                        toolTip = null;
                    }
                });
        }

        toolTip.Content = new TextBlock { Text = data.Message, TextWrapping = TextWrapping.Wrap };

        e.Handled = true;
        ToolTip.SetIsOpen(textEditor, true);
        toolTip.InvalidateVisual();
    }

    /// <summary>
    /// Get ToolTip data to show at the caret position, can be null if no ToolTip should be shown.
    /// </summary>
    private ToolTipData? GetCaretToolTipData(TextViewPosition position)
    {
        var logicalPosition = position.Location;
        var pointerOffset = textEditor.Document.GetOffset(logicalPosition.Line, logicalPosition.Column);

        var line = textEditor.Document.GetLineByOffset(pointerOffset);
        var lineText = textEditor.Document.GetText(line.Offset, line.Length);

        var lineOffset = pointerOffset - line.Offset;
        /*var caret = textEditor.CaretOffset;
        
        // Get the line the caret is on
        var line = textEditor.Document.GetLineByOffset(caret);
        var lineText = textEditor.Document.GetText(line.Offset, line.Length);
        
        var caretAbsoluteOffset = caret - line.Offset;*/

        // Tokenize
        var result = TokenizerProvider!.TokenizeLine(lineText);

        var currentTokenIndex = -1;
        IToken? currentToken = null;
        // Get the token the caret is after
        foreach (var (i, token) in result.Tokens.Enumerate())
        {
            // If we see a line comment token anywhere, return null
            var isComment = token.Scopes.Any(s => s.Contains("comment.line"));
            if (isComment)
            {
                Logger.Trace("Caret is in a comment");
                return null;
            }

            // Find match
            if (lineOffset >= token.StartIndex && lineOffset <= token.EndIndex)
            {
                currentTokenIndex = i;
                currentToken = token;
                break;
            }
        }

        // Still not found
        if (currentToken is null || currentTokenIndex == -1)
        {
            Logger.Info(
                $"Could not find token at pointer offset {pointerOffset} for line {lineText.ToRepr()}"
            );
            return null;
        }

        var startOffset = currentToken.StartIndex + line.Offset;
        var endOffset = currentToken.EndIndex + line.Offset;

        // Cap the offsets by the line offsets
        var segment = new TextSegment
        {
            StartOffset = Math.Max(startOffset, line.Offset),
            EndOffset = Math.Min(endOffset, line.EndOffset)
        };

        // Only return for supported scopes
        // Attempt with first current, then next and previous
        foreach (var tokenOffset in new[] { 0, 1, -1 })
        {
            if (result.Tokens.ElementAtOrDefault(currentTokenIndex + tokenOffset) is { } token)
            {
                // Check supported scopes
                if (token.Scopes.Where(s => s.Contains("invalid")).ToArray() is { Length: > 0 } results)
                {
                    // Special cases
                    if (results.Contains("invalid.illegal.mismatched.parenthesis.closing.prompt"))
                    {
                        return new ToolTipData(segment, "Mismatched closing parenthesis ')'");
                    }
                    if (results.Contains("invalid.illegal.mismatched.parenthesis.opening.prompt"))
                    {
                        return new ToolTipData(segment, "Mismatched opening parenthesis '('");
                    }
                    if (results.Contains("invalid.illegal.expected-weight-separator.prompt"))
                    {
                        return new ToolTipData(segment, "Expected numeric weight");
                    }

                    return new ToolTipData(segment, "Syntax error: " + string.Join(", ", results));
                }
            }
        }

        return null;
    }

    internal record ToolTipData(ISegment Segment, string Message);
}
