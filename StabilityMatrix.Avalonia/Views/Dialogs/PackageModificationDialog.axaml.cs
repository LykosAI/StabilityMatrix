using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Helper;
using TextMateSharp.Grammars;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class PackageModificationDialog : UserControlBase
{
    public PackageModificationDialog()
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

            if (scope is null)
                throw new InvalidOperationException("Scope is null");

            textMate.SetGrammar(scope);
            textMate.SetTheme(options.LoadTheme(ThemeName.DarkPlus));
        }

        EventManager.Instance.ScrollToBottomRequested += (_, _) =>
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var editor = this.FindControl<TextEditor>("Console");
                if (editor?.Document == null)
                    return;
                var line = Math.Max(editor.Document.LineCount - 5, 1);
                editor.ScrollToLine(line);
            });
        };
    }
}
