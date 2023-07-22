using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class ExceptionDialog : AppWindowBase
{
    private CancellationTokenSource? showCts;
    
    public ExceptionDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (showCts is not null)
        {
            showCts.Cancel();
            showCts = null;
        }
    }
    
    /// <summary>
    /// Fallback if ShowDialog is unavailable due to the MainWindow not being visible.
    /// </summary>
    public void ShowWithCts(CancellationTokenSource cts)
    {
        showCts?.Cancel();
        showCts = cts;
        Show();
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
