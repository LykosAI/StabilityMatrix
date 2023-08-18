using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Styles;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace StabilityMatrix.Avalonia.Controls;

public class PromptCard : TemplatedControl
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        InitializeEditors(e);
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
    
    private void InitializeEditors(TemplateAppliedEventArgs e)
    {
        const ThemeName themeName = ThemeName.DimmedMonokai;
        var registryOptions = new RegistryOptions(themeName);
        
        var registry = new Registry(registryOptions);
        
        using var stream = Assets.ImagePromptLanguageJson.Open();
        var promptGrammar = registry.LoadGrammarFromStream(stream);
        
        // Load theme
        var theme = GetCustomTheme();
        
        foreach (var editor in new[]
                 {
                     e.NameScope.Find<TextEditor>("PromptEditor"), 
                     e.NameScope.Find<TextEditor>("NegativePromptEditor")
                 })
        {
            if (editor is not null)
            {
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
                var field = typeof(TextMate.Installation).GetField("_textMateRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
                field!.SetValue(installation, registry);
                
                installation.SetGrammar(promptGrammar.GetScopeName());
                
                installation.SetTheme(theme);
            }
        }
    }
}
