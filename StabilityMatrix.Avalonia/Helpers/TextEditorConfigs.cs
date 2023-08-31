using System.IO;
using System.Reflection;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Styles;
using StabilityMatrix.Core.Extensions;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace StabilityMatrix.Avalonia.Helpers;

public class TextEditorConfigs
{
    public static void ConfigForPrompt(TextEditor editor)
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
