using System;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<OpenArtBrowserPage>]
public partial class OpenArtBrowserPage : UserControlBase
{
    private readonly ISettingsManager settingsManager;

    public OpenArtBrowserPage(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        InitializeComponent();
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        if (scrollViewer.Offset.Y == 0)
            return;

        var isAtEnd = Math.Abs(scrollViewer.Offset.Y - scrollViewer.ScrollBarMaximum.Y) < 1f;

        if (
            isAtEnd
            && settingsManager.Settings.IsWorkflowInfiniteScrollEnabled
            && DataContext is IInfinitelyScroll scroll
        )
        {
            scroll.LoadNextPageAsync().SafeFireAndForget();
        }
    }
}
