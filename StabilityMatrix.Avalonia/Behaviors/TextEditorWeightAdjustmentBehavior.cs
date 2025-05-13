﻿using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.PromptSyntax;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Behaviors;

[Localizable(false)]
public class TextEditorWeightAdjustmentBehavior : Behavior<TextEditor>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private TextEditor? textEditor;

    public static readonly StyledProperty<ITokenizerProvider?> TokenizerProviderProperty =
        AvaloniaProperty.Register<TextEditorWeightAdjustmentBehavior, ITokenizerProvider?>(
            nameof(TokenizerProvider)
        );

    public ITokenizerProvider? TokenizerProvider
    {
        get => GetValue(TokenizerProviderProperty);
        set => SetValue(TokenizerProviderProperty, value);
    }

    public static readonly StyledProperty<double> WeightIncrementProperty = AvaloniaProperty.Register<
        TextEditorWeightAdjustmentBehavior,
        double
    >(nameof(WeightIncrement), 0.1);

    public double WeightIncrement
    {
        get => GetValue(WeightIncrementProperty);
        set => SetValue(WeightIncrementProperty, value);
    }

    public static readonly StyledProperty<double> MinWeightProperty = AvaloniaProperty.Register<
        TextEditorWeightAdjustmentBehavior,
        double
    >(nameof(MinWeight), -10.0);

    public double MinWeight
    {
        get => GetValue(MinWeightProperty);
        set => SetValue(MinWeightProperty, value);
    }

    public static readonly StyledProperty<double> MaxWeightProperty = AvaloniaProperty.Register<
        TextEditorWeightAdjustmentBehavior,
        double
    >(nameof(MaxWeight), 10.0);

    public double MaxWeight
    {
        get => GetValue(MaxWeightProperty);
        set => SetValue(MaxWeightProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is { } editor)
        {
            textEditor = editor;
            textEditor.KeyDown += TextEditor_KeyDown;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (textEditor != null)
        {
            textEditor.KeyDown -= TextEditor_KeyDown;
        }
    }

    private void TextEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        // Control for Win / Cmd for macOS
        var modifier = Compat.IsMacOS ? KeyModifiers.Meta : KeyModifiers.Control;

        if (!e.KeyModifiers.HasFlag(modifier) || e.Key is not (Key.Up or Key.Down))
            return;

        e.Handled = true;

        // Calculate the weight delta
        var delta = e.Key == Key.Up ? WeightIncrement : -WeightIncrement;

        try
        {
            HandleWeightAdjustment(delta);
        }
        catch (Exception exception)
        {
            Logger.Warn(exception, "Failed to handle weight adjustment: {Msg}", exception.Message);
        }
    }

    [Localizable(false)]
    private void HandleWeightAdjustment(double delta)
    {
        if (TokenizerProvider is null || textEditor is null)
            return;

        // Backup the current caret position
        var caretOffset = textEditor.CaretOffset;

        // 1. Determine the range to operate on (selection or caret token).
        var editorSelectionSegment = textEditor.GetSelectionSegment();

        Logger.Info(
            "Adjusting weight ({Delta}) (Caret: {Caret}, Selection: {Selection})",
            delta,
            caretOffset,
            editorSelectionSegment
        );

        try
        {
            // 1. Tokenize the entire text
            var text = textEditor.Document.Text;
            var tokenizeResult = TokenizerProvider.TokenizeLine(text);

            // 2. Get the token segment
            if (
                (
                    editorSelectionSegment != null
                        ? GetSelectedTokenSpan(tokenizeResult)
                        : GetCaretTokenSpan(tokenizeResult)
                )
                is not { } tokenSegment
            )
            {
                Logger.Warn("No token segment found");
                return;
            }

            Logger.Debug("Token segment: {Segment}", tokenSegment);

            // 3. Build the AST
            var astBuilder = new PromptSyntaxBuilder(tokenizeResult, text);
            var ast = astBuilder.BuildAST();

            // Find *all* nodes that contains the selection/caret range
            var selectedNodes = ast.RootNode.Content.Where(node => node.Span.Contains(tokenSegment)).ToList();

            // If empty use intersection instead
            if (selectedNodes.Count == 0)
            {
                selectedNodes = ast
                    .RootNode.Content.Where(node => node.Span.IntersectsWith(tokenSegment))
                    .ToList();
            }

            Logger.Trace("Selected nodes: {Nodes}", selectedNodes);

            // Trim any leading or trailing separator nodes
            selectedNodes = selectedNodes
                .SkipWhile(node => node is SeperatorNode)
                .Reverse()
                .SkipWhile(node => node is SeperatorNode)
                .Reverse()
                .ToList();

            // Find smallest containing nodes
            var smallestNodes = selectedNodes
                .Select(node =>
                {
                    // For nodes that *fully contain* the selection, get smallest containing descendant instead
                    if (!node.Span.Contains(tokenSegment))
                        return node;
                    return node.FindSmallestContainingDescendant(tokenSegment);
                })
                .ToList();

            Logger.Trace("Smallest nodes: {Nodes}", smallestNodes);

            // Go up and find the first parenthesized node, if any
            // (Considering only the first and last of the smallest nodes)
            var parenthesisTargets = smallestNodes.Take(1).ToList();
            if (smallestNodes.Count > 1)
            {
                parenthesisTargets.Add(smallestNodes.Last());
            }

            Logger.Trace("Parenthesis targets: {Nodes}", parenthesisTargets);

            var parenthesizedNode = parenthesisTargets
                .SelectMany(x => x.AncestorsAndSelf())
                .OfType<ParenthesizedNode>()
                .FirstOrDefault();

            // Logger.Trace("Parenthesized node: {Node} of {Nodes}", parenthesizedNode, parenthesisTargets);

            var currentWeight = 1.0;
            int replacementOffset; // Offset to start replacing text
            int replacementLength; // Length of text to replace (0 if inserting)
            string newText;

            if (parenthesizedNode != null)
            {
                // We're inside parentheses.  Get the existing weight, if any.
                currentWeight = (double?)parenthesizedNode.Weight?.Value ?? 1.0;

                // Calculate new weight
                var newWeight = Math.Clamp(currentWeight + delta, MinWeight, MaxWeight);

                if (parenthesizedNode.Weight is not null) // if the weight exists
                {
                    // Replace existing weight
                    replacementOffset = parenthesizedNode.Weight.StartIndex;
                    replacementLength = parenthesizedNode.Weight.Length;
                    newText = FormatWeight(newWeight);
                }
                else
                {
                    // Insert the weight before the closing parenthesis.
                    replacementOffset = parenthesizedNode.EndIndex - 1; // EndIndex is exclusive, so -1
                    replacementLength = 0; // insert
                    newText = $":{FormatWeight(newWeight)}";
                }
            }
            else
            {
                // Not inside parentheses. Wrap the selected tokens and add the weight.
                var selectedText = ast.GetSourceText(tokenSegment);

                var newWeight = Math.Clamp(currentWeight + delta, MinWeight, MaxWeight);

                replacementOffset = tokenSegment.Start;
                replacementLength = tokenSegment.Length;
                newText = $"({selectedText}:{FormatWeight(newWeight)})";
            }

            // 8. Replace the text.
            Logger.Debug(
                "Replacing source text {SrcText} at {SrcRange}, with new text {NewText}",
                text[replacementOffset..(replacementOffset + replacementLength)],
                new TextSpan(replacementOffset, replacementLength),
                newText
            );
            textEditor.Document.Replace(replacementOffset, replacementLength, newText);

            // Plus 1 to caret if we added parenthesis
            if (parenthesizedNode == null)
            {
                caretOffset += 1;
            }

            // 9. Update caret/selection.
            if (editorSelectionSegment is not null)
            {
                // Restore the caret position
                textEditor.CaretOffset = caretOffset;

                // textEditor.SelectionStart = tokenSegment.Offset;
                // TODO: textEditor.SelectionEnd = tokenSegment.Offset + newText.Length;
            }
            else
            {
                // Restore the caret position
                textEditor.CaretOffset = caretOffset;

                // Put it inside the parenthesis
                // textEditor.CaretOffset = replacementOffset + newText.Length;
            }
        }
        catch (Exception exception)
        {
            Logger.Warn(exception, "Failed to handle weight adjustment: {Msg}", exception.Message);
        }
    }

    private TextSpan? GetSelectedTokenSpan(ITokenizeLineResult result)
    {
        if (textEditor is null)
            return null;

        if (textEditor.SelectionLength == 0)
            return null;

        var selectionStart = textEditor.SelectionStart;
        var selectionEnd = selectionStart + textEditor.SelectionLength;

        IToken? startToken = null;
        IToken? endToken = null;

        // Find the tokens that intersect the selection.
        foreach (var token in result.Tokens)
        {
            var tokenStart = token.StartIndex;
            var tokenEnd = token.EndIndex;

            if (tokenEnd > selectionStart && startToken is null)
            {
                startToken = token;
            }
            if (tokenStart <= selectionEnd)
            {
                endToken = token;
            }

            if (tokenStart > selectionEnd || tokenEnd >= selectionEnd)
            {
                break; // Optimization: We've passed the selection, so we can stop.
            }
        }

        if (startToken is null || endToken is null)
            return null;

        // Ensure end index is within length of text
        var endIndex = Math.Min(endToken.EndIndex, textEditor.Document.TextLength);

        return TextSpan.FromBounds(startToken.StartIndex, endIndex);
    }

    private TextSpan? GetCaretTokenSpan(ITokenizeLineResult result)
    {
        var caretAbsoluteOffset = textEditor!.CaretOffset;
        var textEndOffset = textEditor.Document.TextLength;

        IToken? currentToken = null;
        var currentTokenIndex = -1;
        // Get the token the caret is after
        for (var i = 0; i < result.Tokens.Length; i++)
        {
            var token = result.Tokens[i];
            // If we see a line comment token anywhere, return null
            /*var isComment = token.Scopes.Any(s => s.Contains("comment.line"));
            if (isComment)
            {
                return null;
            }*/

            // Find match
            if (caretAbsoluteOffset >= token.StartIndex && caretAbsoluteOffset < token.EndIndex)
            {
                currentToken = token;
                currentTokenIndex = i;
                break;
            }

            // If last token, also allow just start match
            if (i == result.Tokens.Length - 1 && caretAbsoluteOffset >= token.StartIndex)
            {
                currentToken = token;
                currentTokenIndex = i;
                break;
            }
        }

        // Check if the token is a separator, if so check the previous or next token instead
        if (currentToken?.Scopes is { } scopes && scopes.Contains("punctuation.separator.variable.prompt"))
        {
            // Check if we have a prev token
            if (
                result.Tokens.ElementAtOrDefault(currentTokenIndex - 1) is { } prevToken
                && !prevToken.Scopes.Contains("punctuation.separator.variable.prompt")
            )
            {
                Logger.Trace(
                    "Matched on seperator, using previous token: {Current} -> {Prev}",
                    currentToken,
                    prevToken
                );
                currentToken = prevToken;
            }
            // Check if we have a next token
            else if (
                result.Tokens.ElementAtOrDefault(currentTokenIndex + 1) is { } nextToken
                && !nextToken.Scopes.Contains("punctuation.separator.variable.prompt")
            )
            {
                Logger.Trace(
                    "Matched on seperator, using next token: {Current} -> {Next}",
                    currentToken,
                    nextToken
                );
                currentToken = nextToken;
            }
        }

        // Still not found or not "text" token (meta.embedded).
        if (
            currentToken?.Scopes is null
            || !(
                currentToken.Scopes.Contains("meta.embedded")
                || currentToken.Scopes.Contains("meta.structure.array.prompt")
            )
        )
        {
            return null;
        }

        // Cap the offsets by the line offsets
        var startOffset = Math.Max(currentToken.StartIndex, 0);
        var endOffset = Math.Min(currentToken.EndIndex, textEndOffset);

        return TextSpan.FromBounds(startOffset, endOffset);
    }

    [Localizable(false)]
    private static string FormatWeight(double weight)
    {
        // Format the weight to one decimal place
        var formattedWeight = weight.ToString("F1", CultureInfo.InvariantCulture);

        // Strip trailing 0
        if (formattedWeight.EndsWith(".0", StringComparison.InvariantCulture))
        {
            formattedWeight = formattedWeight[..^2];
        }

        return formattedWeight;
    }
}
