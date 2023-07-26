using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Helper;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Views;

public partial class LaunchPageView : UserControlBase
{
    private const int LineOffset = 5;
    
    public LaunchPageView()
    {
        InitializeComponent();
        var editor = this.FindControl<TextEditor>("Console");
        if (editor is not null)
        {
            var options = new RegistryOptions(ThemeName.DarkPlus);
        
            // Config hyperlinks
            editor.TextArea.Options.EnableHyperlinks = true;
            editor.TextArea.Options.RequireControlModifierForHyperlinkClick = false;
            editor.TextArea.TextView.LinkTextForegroundBrush = Brushes.Coral;
        
            var textMate = editor.InstallTextMate(options);
            var scope = options.GetScopeByLanguageId("log");
        
            if (scope is null) throw new InvalidOperationException("Scope is null");
        
            textMate.SetGrammar(scope);
            textMate.SetTheme(options.LoadTheme(ThemeName.DarkPlus));
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        EventManager.Instance.ScrollToBottomRequested -= OnScrollToBottomRequested;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        EventManager.Instance.ScrollToBottomRequested += OnScrollToBottomRequested;
    }

    private void OnScrollToBottomRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var editor = this.FindControl<TextEditor>("Console");
            if (editor?.Document == null) return;
            var line = Math.Max(editor.Document.LineCount - LineOffset, 1);
            editor.ScrollToLine(line);
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
