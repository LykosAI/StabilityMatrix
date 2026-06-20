using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    private async void BrowseFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HuggingFacePageViewModel vm)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Choose destination folder", AllowMultiple = false }
        );

        if (folders.Count > 0)
            vm.FolderImportCustomPath = folders[0].Path.LocalPath;
    }

    private void SearchResults_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        // Load the next page when scrolled to the bottom, OR when the content doesn't fill the
        // viewport (e.g. few results on a large/4K screen, so there's no scrollbar to reach the
        // bottom). Adding items raises ScrollChanged again, so this keeps filling until the
        // viewport is scrollable or there are no more results (LoadNextPageAsync no-ops then).
        var notScrollable = scrollViewer.Extent.Height <= scrollViewer.Viewport.Height;
        var atBottom = Math.Abs(scrollViewer.Offset.Y - scrollViewer.ScrollBarMaximum.Y) < 1f;

        if ((notScrollable || atBottom) && DataContext is IInfinitelyScroll scroll)
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
