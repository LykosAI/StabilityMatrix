using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Markdown.Avalonia;
using Markdown.Avalonia.SyntaxHigh.Extensions;
using Refit;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia;

public static class DialogHelper
{
    /// <summary>
    /// Create a generic textbox entry content dialog.
    /// </summary>
    public static BetterContentDialog CreateTextEntryDialog(
        string title,
        string description,
        IReadOnlyList<TextBoxField> textFields
    )
    {
        Dispatcher.UIThread.VerifyAccess();

        var stackPanel = new StackPanel();
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new TextBlock { Text = description },
                stackPanel
            }
        };
        grid.Loaded += (_, _) =>
        {
            // Focus first textbox
            var firstTextBox = stackPanel.Children.OfType<TextBox>().First();
            firstTextBox.Focus();
            firstTextBox.CaretIndex = firstTextBox.Text?.LastIndexOf('.') ?? 0;
        };

        // Disable primary button if any textboxes are invalid
        var primaryCommand = new RelayCommand(
            delegate { },
            () =>
            {
                var invalidCount = textFields.Count(field => !field.IsValid);
                Debug.WriteLine($"Checking can execute: {invalidCount} invalid fields");
                return invalidCount == 0;
            }
        );

        // Create textboxes
        foreach (var field in textFields)
        {
            var label = new TextBlock { Text = field.Label };
            stackPanel.Children.Add(label);

            var textBox = new TextBox
            {
                [!TextBox.TextProperty] = new Binding("TextProperty"),
                Watermark = field.Watermark,
                DataContext = field,
            };
            stackPanel.Children.Add(textBox);

            // When IsValid property changes, update invalid count and primary button
            field.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(TextBoxField.IsValid))
                {
                    primaryCommand.NotifyCanExecuteChanged();
                }
            };

            // Set initial value
            textBox.Text = field.Text;

            // See if initial value is valid
            try
            {
                field.Validator?.Invoke(field.Text);
            }
            catch (Exception)
            {
                field.IsValid = false;
            }
        }

        return new BetterContentDialog
        {
            Title = title,
            Content = grid,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = true,
            PrimaryButtonCommand = primaryCommand,
            DefaultButton = ContentDialogButton.Primary
        };
    }

    /// <summary>
    /// Create a generic dialog for showing a markdown document
    /// </summary>
    public static BetterContentDialog CreateMarkdownDialog(string markdown, string? title = null)
    {
        Dispatcher.UIThread.VerifyAccess();

        var viewer = new MarkdownScrollViewer { Markdown = markdown };

        return new BetterContentDialog
        {
            Title = title,
            Content = viewer,
            CloseButtonText = "Close",
            IsPrimaryButtonEnabled = false,
        };
    }

    /// <summary>
    /// Create a dialog for displaying an ApiException
    /// </summary>
    public static BetterContentDialog CreateApiExceptionDialog(
        ApiException exception,
        string? title = null
    )
    {
        Dispatcher.UIThread.VerifyAccess();

        // Setup text editor
        var textEditor = new TextEditor
        {
            IsReadOnly = true,
            WordWrap = true,
            Options = { ShowColumnRulers = false, AllowScrollBelowDocument = false }
        };
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        textEditor
            .InstallTextMate(registryOptions)
            .SetGrammar(registryOptions.GetScopeByLanguageId("json"));

        var mainGrid = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text = $"{(int)exception.StatusCode} - {exception.ReasonPhrase}",
                    FontSize = 18,
                    FontWeight = FontWeight.Medium,
                    Margin = new Thickness(0, 8),
                },
                textEditor
            }
        };

        var dialog = new BetterContentDialog
        {
            Title = title,
            Content = mainGrid,
            CloseButtonText = "Close",
            IsPrimaryButtonEnabled = false,
        };

        // Try to deserialize to json element
        if (exception.Content != null)
        {
            try
            {
                // Deserialize to json element then re-serialize to ensure indentation
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(
                    exception.Content,
                    new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    }
                );
                var formatted = JsonSerializer.Serialize(
                    jsonElement,
                    new JsonSerializerOptions() { WriteIndented = true }
                );

                textEditor.Document.Text = formatted;
            }
            catch (JsonException)
            {
                // Otherwise just add the content as a code block
                textEditor.Document.Text = exception.Content;
            }
        }

        return dialog;
    }

    public static BetterContentDialog CreatePromptErrorDialog(
        PromptError exception,
        string sourceText
    )
    {
        Dispatcher.UIThread.VerifyAccess();

        var title =
            exception is PromptSyntaxError ? "Prompt Syntax Error" : "Prompt Validation Error";

        // Get the index of the error
        var errorIndex = exception.TextOffset;

        // Get the line of error
        var total = 0;
        var errorLine = string.Empty;
        var errorLineNum = 0;
        var errorLineOffset = -1;
        var errorLineEndOffset = -1;
        foreach (var (i, line) in sourceText.Split(Environment.NewLine).Enumerate())
        {
            var lineLength = line.Length + Environment.NewLine.Length;
            if (total + lineLength > errorIndex)
            {
                // Found, set the line text and number
                errorLine = line;
                errorLineNum = i + 1;
                // Calculate line offset of the error
                errorLineOffset = exception.TextOffset - total;
                // Calculate line offset of the end of the error
                errorLineEndOffset = exception.TextEndOffset - total;
                break;
            }
            total += lineLength;
        }

        // Format the error line
        var errorLineFormattedBuilder = new StringBuilder();
        // Add line number
        var errorLinePrefix = $"[{errorLineNum}] ";
        errorLineFormattedBuilder.AppendLine(errorLinePrefix + errorLine);
        // Add error indicator at line offset
        errorLineFormattedBuilder.Append(' ', errorLinePrefix.Length + errorLineOffset);
        errorLineFormattedBuilder.Append('^', errorLineEndOffset - errorLineOffset);
        var errorLineFormatted = errorLineFormattedBuilder.ToString();

        // Setup text editor
        var textEditor = new TextEditor
        {
            IsReadOnly = true,
            WordWrap = true,
            IsEnabled = false,
            ShowLineNumbers = false,
            FontFamily = "Cascadia Code,Consolas,Menlo,Monospace",
            FontSize = 15,
            Options =
            {
                HighlightCurrentLine = true,
                ShowColumnRulers = false,
                AllowScrollBelowDocument = false
            }
        };
        TextEditorConfigs.ConfigForPrompt(textEditor);

        textEditor.Document.Text = errorLineFormatted;
        textEditor.TextArea.Caret.Offset = textEditor.Document.Lines[0].EndOffset;

        var mainGrid = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text =
                        $"{exception.Message} - at line {errorLineNum} [{errorLineOffset}:{errorLineEndOffset}]",
                    FontSize = 18,
                    FontWeight = FontWeight.Medium,
                    Margin = new Thickness(0, 8),
                },
                textEditor
            }
        };

        var dialog = new BetterContentDialog
        {
            Title = title,
            Content = mainGrid,
            CloseButtonText = "Close",
            IsPrimaryButtonEnabled = false,
        };

        return dialog;
    }

    /// <summary>
    /// Create a simple title and description task dialog.
    /// Sets the XamlRoot to the current top level window.
    /// </summary>
    public static TaskDialog CreateTaskDialog(string title, string description)
    {
        Dispatcher.UIThread.VerifyAccess();

        var content = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 16,
                    Text = title,
                    TextWrapping = TextWrapping.WrapWithOverflow,
                },
                new TextBlock { Text = description, TextWrapping = TextWrapping.WrapWithOverflow, }
            }
        };

        return new TaskDialog
        {
            Title = title,
            Content = content,
            XamlRoot = App.VisualRoot
        };
    }
}

// Text fields
public sealed class TextBoxField : INotifyPropertyChanged
{
    // Label above the textbox
    public string Label { get; init; } = string.Empty;

    // Actual text value
    public string Text { get; set; } = string.Empty;

    // Watermark text
    public string Watermark { get; init; } = string.Empty;

    /// <summary>
    /// Validation action on text changes. Throw exception if invalid.
    /// </summary>
    public Action<string>? Validator { get; init; }

    public string TextProperty
    {
        get => Text;
        [DebuggerStepThrough]
        set
        {
            try
            {
                Validator?.Invoke(value);
            }
            catch (Exception e)
            {
                IsValid = false;
                throw new DataValidationException(e.Message);
            }
            Text = value;
            IsValid = true;
            OnPropertyChanged();
        }
    }

    // Default to true if no validator is provided
    private bool isValid;
    public bool IsValid
    {
        get => Validator == null || isValid;
        set
        {
            isValid = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
