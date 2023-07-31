using System;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit.TextMate;
using StabilityMatrix.Avalonia.Controls;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Views;

public partial class InferenceTextToImageView : UserControlBase
{
    public InferenceTextToImageView()
    {
        InitializeComponent();
        InitializeEditors();
    }
    
    private void InitializeEditors()
    {
        foreach (var editor in new[] {PromptEditor, NegativePromptEditor})
        {
            if (editor is not null)
            {
                var options = new RegistryOptions(ThemeName.DarkPlus);
        
                var editorOptions = editor.TextArea.Options;
                // Config hyperlinks
                editorOptions.EnableHyperlinks = true;
                editorOptions.RequireControlModifierForHyperlinkClick = true;
                editor.TextArea.TextView.LinkTextForegroundBrush = Brushes.Coral;
        
                var textMate = editor.InstallTextMate(options);
                var scope = options.GetScopeByLanguageId("log");
        
                if (scope is null) throw new InvalidOperationException("Scope is null");
        
                textMate.SetGrammar(scope);
                textMate.SetTheme(options.LoadTheme(ThemeName.DarkPlus));
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
