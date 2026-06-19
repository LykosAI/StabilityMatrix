using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<HuggingFacePage>]
public partial class HuggingFacePage : UserControlBase
{
    public HuggingFacePage()
    {
        InitializeComponent();

        // Track click anchor + handle shift-click range selection across the repo file rows.
        // Anchor is recorded on press; range selection happens on release where we can suppress
        // the checkbox's own toggle (Button skips its click when the release is already handled).
        AddHandler(InputElement.PointerPressedEvent, OnRowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, OnRowPointerReleased, RoutingStrategies.Tunnel);
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (
            !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && DataContext is HuggingFacePageViewModel vm
            && GetFile(e.Source) is { } file
        )
        {
            vm.SetSelectionAnchor(file);
        }
    }

    private void OnRowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (
            e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && DataContext is HuggingFacePageViewModel vm
            && GetFile(e.Source) is { } file
        )
        {
            vm.SelectRangeTo(file);
            e.Handled = true;
        }
    }

    private void SearchResults_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.Offset.Y == 0)
            return;

        var isAtEnd = Math.Abs(scrollViewer.Offset.Y - scrollViewer.ScrollBarMaximum.Y) < 1f;

        if (isAtEnd && DataContext is IInfinitelyScroll scroll)
            _ = scroll.LoadNextPageAsync();
    }

    private static HuggingFaceFileViewModel? GetFile(object? source) =>
        (source as StyledElement)?.DataContext switch
        {
            HuggingFaceFileViewModel file => file,
            HuggingFaceTreeNodeViewModel { File: { } file } => file,
            _ => null,
        };
}
