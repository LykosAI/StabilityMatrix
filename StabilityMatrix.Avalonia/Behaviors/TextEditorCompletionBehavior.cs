using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using NLog;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Avalonia.Models;
using CompletionWindow = StabilityMatrix.Avalonia.Controls.CodeCompletion.CompletionWindow;

namespace StabilityMatrix.Avalonia.Behaviors;

public class TextEditorCompletionBehavior : Behavior<TextEditor>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private TextEditor textEditor = null!;
    
    private CompletionWindow? completionWindow;

    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<TextEditorCompletionBehavior, string>(nameof(Text));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
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
        var window = new CompletionWindow(textArea)
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
        
        var completionList = window.CompletionList;
            
        completionList.CompletionData.Add(new CompletionData("item1"));
        completionList.CompletionData.Add(new CompletionData("item2"));
        completionList.CompletionData.Add(new CompletionData("item3"));
        
        return window;
    }

    private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { } triggerText) return;
        
        if (triggerText.All(char.IsLetterOrDigit))
        {
            // Create completion window if its not already created
            if (completionWindow == null)
            {
                // Get the segment of the token the caret is currently in
                if (GetCaretToken(textEditor) is not { } tokenSegment)
                {
                    Logger.Trace("Token segment not found");
                    return;
                }

                var token = textEditor.Document.GetText(tokenSegment);
                Logger.Trace("Using token {Token} ({@Segment})", token, tokenSegment);
                
                completionWindow = CreateCompletionWindow(textEditor.TextArea);
                completionWindow.StartOffset = tokenSegment.Offset;
                completionWindow.EndOffset = tokenSegment.EndOffset;
                
                completionWindow.CompletionList.SelectItem(token);
                
                completionWindow.Closed += delegate
                {
                    completionWindow = null;
                };
            
                completionWindow.Show();
            }
        }
    }

    private void TextArea_TextEntering(object? sender, TextInputEventArgs e)
    {
        if (completionWindow is null) return;
        
        // When completion window is open, parse and update token offsets
        if (GetCaretToken(textEditor) is not { } tokenSegment)
        {
            Logger.Trace("Token segment not found");
            return;
        }

        completionWindow.StartOffset = tokenSegment.Offset;
        completionWindow.EndOffset = tokenSegment.EndOffset;

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

    /// <summary>
    /// Gets a segment of the token the caret is currently in.
    /// </summary>
    private static ISegment? GetCaretToken(TextEditor textEditor)
    {
        var caret = textEditor.CaretOffset;

        // Search for the start and end of a token
        // A token is defined as either alphanumeric chars or a space
        var start = caret;
        while (start > 0 && char.IsLetterOrDigit(textEditor.Document.GetCharAt(start - 1)))
        {
            start--;
        }
        
        var end = caret;
        while (end < textEditor.Document.TextLength && char.IsLetterOrDigit(textEditor.Document.GetCharAt(end)))
        {
            end++;
        }
        
        return start < end ? new TextSegment { StartOffset = start, EndOffset = end } : null;
    }
}
