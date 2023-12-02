using System;
using System.IO;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Styles;
using StabilityMatrix.Core.Extensions;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace StabilityMatrix.Avalonia.Helpers;

public static class TextEditorConfigs
{
    public static void Configure(TextEditor editor, TextEditorPreset preset)
    {
        switch (preset)
        {
            case TextEditorPreset.Prompt:
                ConfigForPrompt(editor);
                break;
            case TextEditorPreset.Console:
                ConfigForConsole(editor);
                break;
            case TextEditorPreset.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
        }
    }

    private static void ConfigForPrompt(TextEditor editor)
    {
        const ThemeName themeName = ThemeName.DimmedMonokai;
        var registryOptions = new RegistryOptions(themeName);

        var registry = new Registry(registryOptions);

        using var stream = Assets.ImagePromptLanguageJson.Open();
        var promptGrammar = registry.LoadGrammarFromStream(stream);

        // Load theme
        var theme = GetCustomTheme();

        // Setup editor
        var editorOptions = editor.Options;
        editorOptions.ShowColumnRulers = true;
        editorOptions.EnableTextDragDrop = true;
        editorOptions.ExtendSelectionOnMouseUp = true;
        // Config hyperlinks
        editorOptions.EnableHyperlinks = true;
        editorOptions.RequireControlModifierForHyperlinkClick = true;
        editor.TextArea.TextView.LinkTextForegroundBrush = Brushes.Coral;
        editor.TextArea.SelectionBrush = ThemeColors.EditorSelectionBrush;

        var installation = editor.InstallTextMate(registryOptions);

        // Set the _textMateRegistry property
        installation.SetPrivateField("_textMateRegistry", registry);

        installation.SetGrammar(promptGrammar.GetScopeName());

        installation.SetTheme(theme);
    }

    private static void ConfigForConsole(TextEditor editor)
    {
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        // Config hyperlinks
        editor.TextArea.Options.EnableHyperlinks = true;
        editor.TextArea.Options.RequireControlModifierForHyperlinkClick = false;
        editor.TextArea.TextView.LinkTextForegroundBrush = Brushes.Coral;

        var textMate = editor.InstallTextMate(registryOptions);
        var scope = registryOptions.GetScopeByLanguageId("log");

        if (scope is null)
            throw new InvalidOperationException("Scope is null");

        textMate.SetGrammar(scope);
        textMate.SetTheme(registryOptions.LoadTheme(ThemeName.DarkPlus));

        editor.Options.ShowBoxForControlCharacters = false;
    }

    private static IRawTheme GetThemeFromStream(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return ThemeReader.ReadThemeSync(reader);
    }

    private static IRawTheme GetCustomTheme()
    {
        using var stream = Assets.ThemeMatrixDarkJson.Open();
        return GetThemeFromStream(stream);
    }
}
