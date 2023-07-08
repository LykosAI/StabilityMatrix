using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using StabilityMatrix.Avalonia.Controls;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Views;

public partial class LaunchPageView : UserControlBase
{
    public LaunchPageView()
    {
        InitializeComponent();
        var editor = this.FindControl<TextEditor>("Console");
        var options = new RegistryOptions(ThemeName.HighContrastLight);
        
        var textMate = editor.InstallTextMate(options);
        var scope = options.GetScopeByLanguageId("log");
        
        if (scope is null) throw new InvalidOperationException("Scope is null");
        
        textMate.SetGrammar(scope);
        textMate.SetTheme(options.LoadTheme(ThemeName.DarkPlus));
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
