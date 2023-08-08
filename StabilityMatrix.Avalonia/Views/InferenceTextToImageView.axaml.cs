using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.TextMate;
using Markdown.Avalonia.SyntaxHigh.Extensions;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace StabilityMatrix.Avalonia.Views;

public partial class InferenceTextToImageView : UserControlBase
{
    public InferenceTextToImageView()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        InitializeEditors();
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

    private void InitializeEditors()
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
                     this.FindControl<TextEditor>("PromptEditor"), 
                     this.FindControl<TextEditor>("NegativePromptEditor")
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
                
                var installation = editor.InstallTextMate(registryOptions);
                
                // Set the _textMateRegistry property
                var field = typeof(TextMate.Installation).GetField("_textMateRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
                field!.SetValue(installation, registry);
                
                installation.SetGrammar(promptGrammar.GetScopeName());
                
                installation.SetTheme(theme);
            }
        }
    }
    
    /*private void InitializeEditorsForXshd()
    {
        var highlightManager = HighlightingManager.Instance;
        
        using var stream = Assets.SDPromptXshd.Open();
        using var reader = new XmlTextReader(stream);
        
        highlightManager.RegisterHighlighting(
            "ImagePrompt", 
            new []{ ".prompt" }, 
            HighlightingLoader.Load(reader, HighlightingManager.Instance));
        
        const ThemeName theme = ThemeName.DimmedMonokai;
        
        foreach (var editor in new[]
                 {
                     this.FindControl<TextEditor>("PromptEditor"), 
                     this.FindControl<TextEditor>("NegativePromptEditor")
                 })
        {
            if (editor is not null)
            {
                var editorOptions = editor.TextArea.Options;
                // Config hyperlinks
                editorOptions.EnableHyperlinks = true;
                editorOptions.RequireControlModifierForHyperlinkClick = true;
                editor.TextArea.TextView.LinkTextForegroundBrush = Brushes.Coral;
                
                editor.SyntaxHighlighting = highlightManager.GetDefinition("ImagePrompt");
            }
        }
    }*/


}
