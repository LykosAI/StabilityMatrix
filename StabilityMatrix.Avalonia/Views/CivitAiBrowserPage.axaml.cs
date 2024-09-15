using System;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Attributes;
using CivitAiBrowserViewModel = StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser.CivitAiBrowserViewModel;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class CivitAiBrowserPage : ResizableUserControlBase
{
    public CivitAiBrowserPage()
    {
        InitializeComponent();
    }

    protected override Action OnResizeFactorChanged =>
        () =>
        {
            ImageRepeater.InvalidateMeasure();
            ImageRepeater.InvalidateArrange();
        };

    protected override double MinResizeFactor => 0.6d;
    protected override double MaxResizeFactor => 1.5d;

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        if (scrollViewer.Offset.Y == 0)
            return;

        var isAtEnd = Math.Abs(scrollViewer.Offset.Y - scrollViewer.ScrollBarMaximum.Y) < 1f;

        if (isAtEnd && DataContext is IInfinitelyScroll scroll)
        {
            scroll.LoadNextPageAsync().SafeFireAndForget();
        }
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is CivitAiBrowserViewModel viewModel)
        {
            viewModel.ClearSearchQuery();
        }
    }
}
