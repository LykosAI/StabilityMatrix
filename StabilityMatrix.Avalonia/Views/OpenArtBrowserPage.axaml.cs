using System;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class OpenArtBrowserPage : UserControlBase
{
    public OpenArtBrowserPage()
    {
        InitializeComponent();
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        if (scrollViewer.Offset.Y == 0)
            return;

        var isAtEnd = Math.Abs(scrollViewer.Offset.Y - scrollViewer.ScrollBarMaximum.Y) < 0.1f;
        if (isAtEnd && DataContext is IInfinitelyScroll scroll)
        {
            scroll.LoadNextPageAsync().SafeFireAndForget();
        }
    }
}
