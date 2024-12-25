using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using Refit;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services;
using TextMateSharp.Grammars;
using Process = FuzzySharp.Process;

namespace StabilityMatrix.Avalonia;

public static class DialogHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Create and show a generic textbox entry content dialog. Returns the result of the dialog.
    /// </summary>
    public static async Task<ContentDialogValueResult<TextBoxField>> GetTextEntryDialogResultAsync(
        TextBoxField field,
        string title = "",
        string description = ""
    )
    {
        var dialog = CreateTextEntryDialog(title: title, description: description, textFields: [field]);
        var result = await dialog.ShowAsync();

        return new ContentDialogValueResult<TextBoxField>(result, field);
    }

    /// <summary>
    /// Create and show a generic textbox entry content dialog. Returns the result of the dialog.
    /// </summary>
    public static async Task<
        ContentDialogValueResult<IReadOnlyList<TextBoxField>>
    > GetTextEntryDialogResultAsync(
        IReadOnlyList<TextBoxField> fields,
        string title = "",
        string description = ""
    )
    {
        var dialog = CreateTextEntryDialog(title: title, description: description, textFields: fields);
        var result = await dialog.ShowAsync();

        return new ContentDialogValueResult<IReadOnlyList<TextBoxField>>(result, fields);
    }

    /// <summary>
    /// Create a generic textbox entry content dialog.
    /// </summary>
    public static BetterContentDialog CreateTextEntryDialog(
        string title,
        string description,
        IReadOnlyList<TextBoxField> textFields
    )
    {
        return CreateTextEntryDialog(
            title,
            new BetterMarkdownScrollViewer { Markdown = description },
            textFields
        );
    }

    /// <summary>
    /// Create a generic textbox entry content dialog.
    /// </summary>
    public static BetterContentDialog CreateTextEntryDialog(
        string title,
        string description,
        string imageSource,
        IReadOnlyList<TextBoxField> textFields
    )
    {
        var markdown = new BetterMarkdownScrollViewer { Markdown = description };
        var image = new BetterAdvancedImage((Uri?)null)
        {
            Source = imageSource,
            Stretch = Stretch.UniformToFill,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 400,
        };

        Grid.SetRow(markdown, 0);
        Grid.SetRow(image, 1);

        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { markdown, image }
        };

        return CreateTextEntryDialog(title, grid, textFields);
    }

    /// <summary>
    /// Create a generic textbox entry content dialog.
    /// </summary>
    public static BetterContentDialog CreateTextEntryDialog(
        string title,
        Control content,
        IReadOnlyList<TextBoxField> textFields
    )
    {
        Dispatcher.UIThread.VerifyAccess();

        var stackPanel = new StackPanel { Spacing = 4 };

        Grid.SetRow(content, 0);
        Grid.SetRow(stackPanel, 1);

        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Children = { content, stackPanel }
        };
        grid.Loaded += (_, _) =>
        {
            // Focus first TextBox
            var firstTextBox = stackPanel
                .Children.OfType<StackPanel>()
                .FirstOrDefault()
                .FindDescendantOfType<TextBox>();
            firstTextBox!.Focus();
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
            var label = new TextBlock { Text = field.Label, Margin = new Thickness(0, 0, 0, 4) };

            var textBox = new TextBox
            {
                [!TextBox.TextProperty] = new Binding("TextProperty"),
                Watermark = field.Watermark,
                DataContext = field,
            };

            if (field.MinWidth.HasValue)
            {
                textBox.MinWidth = field.MinWidth.Value;
            }

            if (!string.IsNullOrEmpty(field.InnerLeftText))
            {
                textBox.InnerLeftContent = new TextBlock()
                {
                    Text = field.InnerLeftText,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, -4, 0)
                };
            }

            stackPanel.Children.Add(new StackPanel { Spacing = 4, Children = { label, textBox } });

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
            PrimaryButtonText = Resources.Action_OK,
            CloseButtonText = Resources.Action_Cancel,
            IsPrimaryButtonEnabled = true,
            PrimaryButtonCommand = primaryCommand,
            DefaultButton = ContentDialogButton.Primary
        };
    }

    /// <summary>
    /// Create a generic dialog for showing a markdown document
    /// </summary>
    public static BetterContentDialog CreateMarkdownDialog(
        string markdown,
        string? title = null,
        TextEditorPreset editorPreset = default
    )
    {
        Dispatcher.UIThread.VerifyAccess();

        var viewer = new BetterMarkdownScrollViewer { Markdown = markdown };

        // Apply syntax highlighting to code blocks if preset is provided
        if (editorPreset != default)
        {
            using var _ = CodeTimer.StartDebug();

            var appliedCount = 0;

            if (
                viewer.GetLogicalDescendants().FirstOrDefault()?.GetLogicalDescendants() is
                { } stackDescendants
            )
            {
                foreach (var editor in stackDescendants.OfType<TextEditor>())
                {
                    TextEditorConfigs.Configure(editor, editorPreset);

                    editor.FontFamily = "Cascadia Code,Consolas,Menlo,Monospace";
                    editor.Margin = new Thickness(0);
                    editor.Padding = new Thickness(4);
                    editor.IsEnabled = false;

                    if (editor.GetLogicalParent() is Border border)
                    {
                        border.BorderThickness = new Thickness(0);
                        border.CornerRadius = new CornerRadius(4);
                    }

                    appliedCount++;
                }
            }

            Logger.Log(
                appliedCount > 0 ? LogLevel.Trace : LogLevel.Warn,
                $"Applied syntax highlighting to {appliedCount} code blocks"
            );
        }

        return new BetterContentDialog
        {
            Title = title,
            Content = viewer,
            CloseButtonText = Resources.Action_Close,
            IsPrimaryButtonEnabled = false,
            MinDialogWidth = 800,
            MaxDialogHeight = 1000,
            MaxDialogWidth = 1000
        };
    }

    /// <summary>
    /// Create a dialog for displaying an ApiException
    /// </summary>
    public static BetterContentDialog CreateApiExceptionDialog(ApiException exception, string? title = null)
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
        textEditor.InstallTextMate(registryOptions).SetGrammar(registryOptions.GetScopeByLanguageId("json"));

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

    /// <summary>
    /// Create a dialog for displaying json
    /// </summary>
    public static BetterContentDialog CreateJsonDialog(
        string json,
        string? title = null,
        string? subTitle = null
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
        textEditor.InstallTextMate(registryOptions).SetGrammar(registryOptions.GetScopeByLanguageId("json"));

        var mainGrid = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            Children = { textEditor }
        };

        if (subTitle is not null)
        {
            mainGrid.Children.Insert(
                0,
                new TextBlock
                {
                    Text = subTitle,
                    FontSize = 18,
                    FontWeight = FontWeight.Medium,
                    Margin = new Thickness(0, 8),
                }
            );
        }

        var dialog = new BetterContentDialog
        {
            Title = title,
            Content = mainGrid,
            CloseButtonText = "Close",
            PrimaryButtonText = "Copy",
            IsPrimaryButtonEnabled = false,
        };

        // Try to deserialize to json element
        try
        {
            // Deserialize to json element then re-serialize to ensure indentation
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(
                json,
                new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                }
            );
            var formatted = JsonSerializer.Serialize(
                jsonElement,
                new JsonSerializerOptions { WriteIndented = true }
            );

            textEditor.Document.Text = formatted;
        }
        catch (JsonException)
        {
            // Otherwise just add the content as a code block
            textEditor.Document.Text = json;
        }

        dialog.PrimaryButtonCommand = new AsyncRelayCommand(async () =>
        {
            // Copy the json to clipboard
            var clipboard = App.Clipboard;
            await clipboard.SetTextAsync(textEditor.Document.Text);
        });

        return dialog;
    }

    /// <summary>
    /// Create a dialog for displaying a prompt error
    /// </summary>
    /// <param name="exception">Target exception to display</param>
    /// <param name="sourceText">Full text of the target Document</param>
    /// <param name="modelIndexService">Optional model index service to look for similar names</param>
    public static BetterContentDialog CreatePromptErrorDialog(
        PromptError exception,
        string sourceText,
        IModelIndexService? modelIndexService = null
    )
    {
        Dispatcher.UIThread.VerifyAccess();

        var title = exception is PromptSyntaxError ? "Prompt Syntax Error" : "Prompt Validation Error";

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
            WordWrap = false,
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
        TextEditorConfigs.Configure(textEditor, TextEditorPreset.Prompt);

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

        // Check model typos
        if (modelIndexService is not null && exception is PromptUnknownModelError unknownModelError)
        {
            var sharedFolderType = unknownModelError.ModelType.ConvertTo<SharedFolderType>();
            if (modelIndexService.ModelIndex.TryGetValue(sharedFolderType, out var models))
            {
                var result = Process.ExtractOne(
                    unknownModelError.ModelName,
                    models.Select(m => m.FileNameWithoutExtension)
                );

                if (result is { Score: > 40 })
                {
                    mainGrid.Children.Add(
                        new InfoBar
                        {
                            Title = $"Did you mean: {result.Value}?",
                            IsClosable = false,
                            IsOpen = true
                        }
                    );
                }
            }
        }

        textEditor.ScrollToHorizontalOffset(errorLineEndOffset - errorLineOffset);

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
                    Margin = new Thickness(0, 2, 0, 8),
                    FontSize = 20,
                    FontWeight = FontWeight.DemiBold,
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

    // Inner left value
    public string? InnerLeftText { get; init; }

    public int? MinWidth { get; init; }

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
