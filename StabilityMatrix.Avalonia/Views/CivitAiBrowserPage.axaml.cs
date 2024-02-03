using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;
using CivitAiBrowserViewModel = StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser.CivitAiBrowserViewModel;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class CivitAiBrowserPage : UserControlBase
{
    public CivitAiBrowserPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        var isAtEnd = scrollViewer.Offset == scrollViewer.ScrollBarMaximum;
        Debug.WriteLine($"IsAtEnd: {isAtEnd}");
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is CivitAiBrowserViewModel viewModel)
        {
            viewModel.ClearSearchQuery();
        }
    }
}
