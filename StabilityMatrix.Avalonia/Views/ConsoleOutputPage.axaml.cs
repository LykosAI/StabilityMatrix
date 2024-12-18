using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Views;

[RegisterTransient<ConsoleOutputPage>]
public partial class ConsoleOutputPage : UserControlBase
{
    private const int LineOffset = 5;

    public ConsoleOutputPage()
    {
        InitializeComponent();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TextEditorConfigs.Configure(Console, TextEditorPreset.Console);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        EventManager.Instance.ScrollToBottomRequested -= OnScrollToBottomRequested;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        EventManager.Instance.ScrollToBottomRequested += OnScrollToBottomRequested;
        base.OnLoaded(e);
    }

    private void OnScrollToBottomRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var editor = this.FindControl<TextEditor>("Console");
            if (editor?.Document == null)
                return;
            var line = Math.Max(editor.Document.LineCount - LineOffset, 1);
            editor.ScrollToLine(line);
        });
    }
}
