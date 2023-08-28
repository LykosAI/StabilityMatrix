using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using NLog;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Extensions;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Behaviors;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class TextEditorCompletionBehavior : Behavior<TextEditor>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private TextEditor textEditor = null!;
    
    private CompletionWindow? completionWindow;
    
    public static readonly StyledProperty<ICompletionProvider?> CompletionProviderProperty =
        AvaloniaProperty.Register<TextEditorCompletionBehavior, ICompletionProvider?>(nameof(CompletionProvider));

    public ICompletionProvider? CompletionProvider
    {
        get => GetValue(CompletionProviderProperty);
        set => SetValue(CompletionProviderProperty, value);
    }

    public static readonly StyledProperty<ITokenizerProvider?> TokenizerProviderProperty = 
        AvaloniaProperty.Register<TextEditorCompletionBehavior, ITokenizerProvider?>(
        "TokenizerProvider");

    public ITokenizerProvider? TokenizerProvider
    {
        get => GetValue(TokenizerProviderProperty);
        set => SetValue(TokenizerProviderProperty, value);
    }

    public static readonly StyledProperty<bool> IsEnabledProperty = AvaloniaProperty.Register<TextEditorCompletionBehavior, bool>(
        "IsEnabled", true);

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
        textEditor.TextArea.TextEntered += TextArea_TextEntered;
        textEditor.TextArea.TextEntering += TextArea_TextEntering;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        textEditor.TextArea.TextEntered -= TextArea_TextEntered;
        textEditor.TextArea.TextEntering -= TextArea_TextEntering;
    }

    private CompletionWindow CreateCompletionWindow(TextArea textArea)
    {
        var window = new CompletionWindow(textArea, CompletionProvider!, TokenizerProvider!)
        {
            WindowManagerAddShadowHint = false,
            CloseWhenCaretAtBeginning = true,
            CloseAutomatically = true,
            IsLightDismissEnabled = true,
            CompletionList =
            {
                IsFiltering = true
            }
        };
        return window;
    }

    private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        if (!IsEnabled || e.Text is not { } triggerText) return;
        
        if (triggerText.All(IsCompletionChar))
        {
            // Create completion window if its not already created
            if (completionWindow == null)
            {
                // Get the segment of the token the caret is currently in
                if (GetCaretCompletionToken() is not { } tokenSegment)
                {
                    Logger.Trace("Token segment not found");
                    return;
                }

                var token = textEditor.Document.GetText(tokenSegment);
                Logger.Trace("Using token {Token} ({@Segment})", token, tokenSegment);
                
                completionWindow = CreateCompletionWindow(textEditor.TextArea);
                completionWindow.StartOffset = tokenSegment.Offset;
                completionWindow.EndOffset = tokenSegment.EndOffset;

                completionWindow.UpdateQuery(token);
                
                completionWindow.Closed += delegate
                {
                    completionWindow = null;
                };
            
                completionWindow.Show();
            }
        }
        else
        {
            // Disallowed chars, close completion window if its open
            Logger.Trace($"Closing completion window: '{triggerText}' not a valid completion char");
            completionWindow?.Close();
        }
    }

    /// <summary>
    /// Highlights the text segment in the text editor
    /// </summary>
    private void HighlightTextSegment(ISegment segment)
    {
        textEditor.TextArea.Selection = Selection.Create(textEditor.TextArea, segment);
    }

    private void TextArea_TextEntering(object? sender, TextInputEventArgs e)
    {
        if (completionWindow is null) return;
        
        /*Dispatcher.UIThread.Post(() =>
        {
            // When completion window is open, parse and update token offsets
            if (GetCaretToken(textEditor) is not { } tokenSegment)
            {
                Logger.Trace("Token segment not found");
                return;
            }

            completionWindow.StartOffset = tokenSegment.Offset;
            completionWindow.EndOffset = tokenSegment.EndOffset;
        });*/

        /*if (e.Text?.Length > 0) {
            if (!char.IsLetterOrDigit(e.Text[0])) {
                // Whenever a non-letter is typed while the completion window is open,
                // insert the currently selected element.
                completionWindow?.CompletionList.RequestInsertion(e);
            }
        }*/
        // Do not set e.Handled=true.
        // We still want to insert the character that was typed.
    }

    private static bool IsCompletionChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '-';
    }
    
    /// <summary>
    /// Gets a segment of the token the caret is currently in for completions.
    /// Returns null if caret is not on a valid completion token (i.e. comments)
    /// </summary>
    private ISegment? GetCaretCompletionToken()
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
        return new TextSegment
        {
            StartOffset = Math.Max(startOffset, line.Offset),
            EndOffset = Math.Min(endOffset, line.EndOffset)
        };

        // Search for the start and end of a token
        // A token is defined as either alphanumeric chars or a space
        /*var start = caret;
        while (start > 0 && IsCompletionChar(textEditor.Document.GetCharAt(start - 1)))
        {
            start--;
        }

        var end = caret;
        while (end < textEditor.Document.TextLength && IsCompletionChar(textEditor.Document.GetCharAt(end)))
        {
            end++;
        }

        return start < end ? new TextSegment { StartOffset = start, EndOffset = end } : null;*/
    }
}
