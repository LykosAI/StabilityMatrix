﻿using System;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using CivitAiBrowserViewModel = StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser.CivitAiBrowserViewModel;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<CivitAiBrowserPage>]
public partial class CivitAiBrowserPage : UserControlBase
{
    public CivitAiBrowserPage()
    {
        InitializeComponent();

        AcceleratorButtonTeachingTip.Target = AcceleratorButton;
        AcceleratorButton.Click += (s, e) =>
        {
            AcceleratorButtonTeachingTip.IsOpen ^= true;
        };
    }

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
