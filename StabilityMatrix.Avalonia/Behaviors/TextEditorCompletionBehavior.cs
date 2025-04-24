using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using NLog;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Tokens;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Behaviors;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class TextEditorCompletionBehavior : Behavior<TextEditor>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private TextEditor textEditor = null!;

    /// <summary>
    /// The current completion window, if open.
    /// Is set to null when the window is closed.
    /// </summary>
    private CompletionWindow? completionWindow;

    public static readonly StyledProperty<ICompletionProvider?> CompletionProviderProperty =
        AvaloniaProperty.Register<TextEditorCompletionBehavior, ICompletionProvider?>(
            nameof(CompletionProvider)
        );

    public ICompletionProvider? CompletionProvider
    {
        get => GetValue(CompletionProviderProperty);
        set => SetValue(CompletionProviderProperty, value);
    }

    public static readonly StyledProperty<ITokenizerProvider?> TokenizerProviderProperty =
        AvaloniaProperty.Register<TextEditorCompletionBehavior, ITokenizerProvider?>("TokenizerProvider");

    public ITokenizerProvider? TokenizerProvider
    {
        get => GetValue(TokenizerProviderProperty);
        set => SetValue(TokenizerProviderProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is not { } editor)
        {
            throw new NullReferenceException("AssociatedObject is null");
        }

        textEditor = editor;
        textEditor.TextArea.TextEntered += TextArea_TextEntered;
        textEditor.TextArea.KeyDown += TextArea_KeyDown;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        textEditor.TextArea.TextEntered -= TextArea_TextEntered;
        textEditor.TextArea.KeyDown -= TextArea_KeyDown;
    }

    private CompletionWindow CreateCompletionWindow(TextArea textArea)
    {
        var window = new CompletionWindow(textArea, CompletionProvider!, TokenizerProvider!)
        {
            WindowManagerAddShadowHint = false,
            CloseWhenCaretAtBeginning = true,
            CloseAutomatically = true,
            IsLightDismissEnabled = true,
            CompletionList = { IsFiltering = true }
        };
        return window;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void InvokeManualCompletion()
    {
        if (CompletionProvider is null)
        {
            throw new NullReferenceException("CompletionProvider is null");
        }

        // If window already open, skip since handled by completion window
        // Unless this is an end char, where we'll open a new window
        if (completionWindow is { ToolTipIsOpen: true })
        {
            Logger.ConditionalTrace("Skipping, completion window already open");
            return;
        }
        completionWindow?.Hide();
        completionWindow = null;

        // Get the segment of the token the caret is currently in
        if (GetCaretCompletionToken() is not { } completionRequest)
        {
            Logger.ConditionalTrace("Token segment not found");
            return;
        }

        // If type is not available, skip
        if (!CompletionProvider.AvailableCompletionTypes.HasFlag(completionRequest.Type))
        {
            Logger.ConditionalTrace(
                "Skipping, completion type {CompletionType} not available in {AvailableTypes}",
                completionRequest.Type,
                CompletionProvider.AvailableCompletionTypes
            );
            return;
        }

        var tokenSegment = completionRequest.Segment;

        var token = textEditor.Document.GetText(tokenSegment);
        Logger.ConditionalTrace("Using token {Token} ({@Segment})", token, tokenSegment);

        var newWindow = CreateCompletionWindow(textEditor.TextArea);
        newWindow.StartOffset = tokenSegment.Offset;
        newWindow.EndOffset = tokenSegment.EndOffset;

        newWindow.UpdateQuery(completionRequest);

        newWindow.Closed += CompletionWindow_OnClosed;

        completionWindow = newWindow;

        newWindow.Show();
    }

    private void CompletionWindow_OnClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, completionWindow))
        {
            completionWindow = null;
        }

        Logger.ConditionalTrace("Completion window closed");

        if (sender is CompletionWindow window)
        {
            window.Closed -= CompletionWindow_OnClosed;
        }
    }

    private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        Logger.ConditionalTrace("Text entered: {Text}", e.Text);

        if (!IsEnabled || CompletionProvider is null)
        {
            Logger.ConditionalTrace("Skipping, not enabled");
            return;
        }

        if (e.Text is not { } triggerText)
        {
            Logger.ConditionalTrace("Skipping, null trigger text");
            return;
        }

        if (!triggerText.All(IsCompletionChar))
        {
            Logger.ConditionalTrace($"Skipping, invalid trigger text: {triggerText.ToRepr()}");
            return;
        }

        Dispatcher.UIThread.Post(InvokeManualCompletion, DispatcherPriority.Input);
    }

    private void TextArea_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e is { Key: Key.Space, KeyModifiers: KeyModifiers.Control })
        {
            InvokeManualCompletion();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Highlights the text segment in the text editor
    /// </summary>
    private void HighlightTextSegment(ISegment segment)
    {
        textEditor.TextArea.Selection = Selection.Create(textEditor.TextArea, segment);
    }

    private static bool IsCompletionChar(char c)
    {
        const string extraAllowedChars = "._-:<";
        return char.IsLetterOrDigit(c) || extraAllowedChars.Contains(c);
    }

    private static bool IsCompletionEndChar(char c)
    {
        const string endChars = ":";
        return endChars.Contains(c);
    }

    /// <summary>
    /// Gets a segment of the token the caret is currently in for completions.
    /// Returns null if caret is not on a valid completion token (i.e. comments)
    /// </summary>
    private EditorCompletionRequest? GetCaretCompletionToken()
    {
        var caret = textEditor.CaretOffset;

        // Get the line the caret is on
        var line = textEditor.Document.GetLineByOffset(caret);
        var lineText = textEditor.Document.GetText(line.Offset, line.Length);

        var caretAbsoluteOffset = caret - line.Offset;

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
            if (caretAbsoluteOffset >= token.StartIndex && caretAbsoluteOffset <= token.EndIndex)
            {
                currentTokenIndex = i;
                currentToken = token;
                break;
            }
        }

        // Still not found
        if (currentToken is null || currentTokenIndex == -1)
        {
            Logger.Info($"Could not find token at caret offset {caret} for line {lineText.ToRepr()}");
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

        // Check if this is an extra network request
        if (currentToken.Scopes.Contains("meta.structure.network.prompt"))
        {
            // (case for initial '<')
            // - Current token is "punctuation.definition.network.begin.prompt"
            if (currentToken.Scopes.Contains("punctuation.definition.network.begin.prompt"))
            {
                // Offset the segment
                var offsetSegment = new TextSegment
                {
                    StartOffset = segment.StartOffset + 1,
                    EndOffset = segment.EndOffset
                };
                return new EditorCompletionRequest
                {
                    Text = "",
                    Segment = offsetSegment,
                    Type = CompletionType.ExtraNetworkType
                };
            }

            // Next steps require a previous token
            if (result.Tokens.ElementAtOrDefault(currentTokenIndex - 1) is not { } prevToken)
            {
                return null;
            }

            // (case for initial '<type')
            // - Current token has "meta.embedded.network.type.prompt"
            if (currentToken.Scopes.Contains("meta.embedded.network.type.prompt"))
            {
                return new EditorCompletionRequest
                {
                    Text = textEditor.Document.GetText(segment),
                    Segment = segment,
                    Type = CompletionType.ExtraNetworkType
                };
            }

            // (case for initial '<type:')
            // - Current token has "meta.structure.network" and "punctuation.separator.variable"
            // - Previous token has "meta.structure.network" and "meta.embedded.network.type"
            if (
                currentToken.Scopes.Contains("punctuation.separator.variable.prompt")
                && prevToken.Scopes.Contains("meta.structure.network.prompt")
                && prevToken.Scopes.Contains("meta.embedded.network.type.prompt")
            )
            {
                var networkToken = textEditor.Document.GetText(
                    prevToken.StartIndex + line.Offset,
                    prevToken.Length
                );

                PromptExtraNetworkType? networkTypeResult = networkToken.ToLowerInvariant() switch
                {
                    "lora" => PromptExtraNetworkType.Lora,
                    "lyco" => PromptExtraNetworkType.LyCORIS,
                    "embedding" => PromptExtraNetworkType.Embedding,
                    _ => null
                };

                if (networkTypeResult is not { } networkType)
                {
                    return null;
                }

                // Use offset segment to not replace the ':'
                var offsetSegment = new TextSegment
                {
                    StartOffset = segment.StartOffset + 1,
                    EndOffset = segment.EndOffset
                };

                return new EditorCompletionRequest
                {
                    Text = "",
                    Segment = offsetSegment,
                    Type = CompletionType.ExtraNetwork,
                    ExtraNetworkTypes = networkType,
                };
            }

            // (case for already in model token '<type:network')
            // - Current token has "meta.embedded.network.model"
            if (currentToken.Scopes.Contains("meta.embedded.network.model.prompt"))
            {
                var secondPrevToken = result.Tokens.ElementAtOrDefault(currentTokenIndex - 2);
                if (secondPrevToken is null)
                {
                    return null;
                }

                var networkToken = textEditor.Document.GetText(
                    secondPrevToken.StartIndex + line.Offset,
                    secondPrevToken.Length
                );

                PromptExtraNetworkType? networkTypeResult = networkToken.ToLowerInvariant() switch
                {
                    "lora" => PromptExtraNetworkType.Lora,
                    "lyco" => PromptExtraNetworkType.LyCORIS,
                    "embedding" => PromptExtraNetworkType.Embedding,
                    _ => null
                };

                if (networkTypeResult is not { } networkType)
                {
                    return null;
                }

                return new EditorCompletionRequest
                {
                    Text = textEditor.Document.GetText(segment),
                    Segment = segment,
                    Type = CompletionType.ExtraNetwork,
                    ExtraNetworkTypes = networkType,
                };
            }
        }

        // Otherwise treat as tag
        return new EditorCompletionRequest
        {
            Text = textEditor.Document.GetText(segment),
            Segment = segment,
            Type = CompletionType.Tag
        };
    }
}
