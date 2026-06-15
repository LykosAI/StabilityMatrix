using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<BananaVisionPage>]
public partial class BananaVisionPage : UserControlBase
{
    /// <summary>
    /// Threshold in pixels from the bottom to consider the scroll position "near bottom"
    /// </summary>
    private const double ScrollBottomThreshold = 120;

    private ScrollViewer? messageScrollViewer;
    private Button? scrollToBottomButton;
    private bool isNearBottom = true;

    private static bool IsScrollNearBottom(
        ScrollViewer scrollViewer,
        double thresholdPixels = ScrollBottomThreshold
    )
    {
        var extentHeight = scrollViewer.Extent.Height;
        var viewportHeight = scrollViewer.Viewport.Height;
        var offsetY = scrollViewer.Offset.Y;

        if (extentHeight <= 0 || viewportHeight <= 0)
            return true;

        return offsetY + viewportHeight >= extentHeight - thresholdPixels;
    }

    /// <summary>
    /// Supported image extensions for drag and drop
    /// </summary>
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif",
    };

    public BananaVisionPage()
    {
        InitializeComponent();

        // Enable drag and drop
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Handle keyboard events for paste
        AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: true);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Set the StorageProvider on the ViewModel
        if (DataContext is BananaVisionPageViewModel viewModel)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                viewModel.StorageProvider = topLevel.StorageProvider;
            }

            // Subscribe to scroll request events from ViewModel
            viewModel.ScrollToEndRequested += OnScrollToEndRequested;
            viewModel.ScrollToEndForcedRequested += OnScrollToEndForcedRequested;
        }

        // Find the message scroll viewer
        messageScrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
        scrollToBottomButton = this.FindControl<Button>("ScrollToBottomButton");

        if (messageScrollViewer != null)
        {
            messageScrollViewer.ScrollChanged += OnMessageScrollChanged;
            isNearBottom = IsScrollNearBottom(messageScrollViewer);
        }

        if (scrollToBottomButton != null)
        {
            scrollToBottomButton.Click += OnScrollToBottomClicked;
            scrollToBottomButton.IsVisible = false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Unsubscribe from ViewModel events
        if (DataContext is BananaVisionPageViewModel viewModel)
        {
            viewModel.ScrollToEndRequested -= OnScrollToEndRequested;
            viewModel.ScrollToEndForcedRequested -= OnScrollToEndForcedRequested;
        }

        if (messageScrollViewer != null)
        {
            messageScrollViewer.ScrollChanged -= OnMessageScrollChanged;
        }

        if (scrollToBottomButton != null)
        {
            scrollToBottomButton.Click -= OnScrollToBottomClicked;
        }
    }

    /// <summary>
    /// Handles scroll to end requests from the ViewModel
    /// </summary>
    private void OnScrollToEndRequested(object? sender, EventArgs e)
    {
        if (messageScrollViewer == null)
            return;

        isNearBottom = IsScrollNearBottom(messageScrollViewer);

        if (isNearBottom)
        {
            messageScrollViewer.ScrollToEnd();
            if (scrollToBottomButton != null)
            {
                scrollToBottomButton.IsVisible = false;
            }
            return;
        }

        if (scrollToBottomButton != null)
        {
            scrollToBottomButton.IsVisible = true;
        }
    }

    private void OnScrollToEndForcedRequested(object? sender, EventArgs e)
    {
        if (messageScrollViewer == null)
            return;

        messageScrollViewer.ScrollToEnd();
        isNearBottom = true;

        if (scrollToBottomButton != null)
        {
            scrollToBottomButton.IsVisible = false;
        }
    }

    private void OnMessageScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (messageScrollViewer == null)
            return;

        isNearBottom = IsScrollNearBottom(messageScrollViewer);

        if (isNearBottom)
        {
            if (scrollToBottomButton != null)
            {
                scrollToBottomButton.IsVisible = false;
            }
        }
        else
        {
            // If the user scrolls up manually, keep the affordance visible so they can jump back down.
            if (scrollToBottomButton != null)
            {
                scrollToBottomButton.IsVisible = true;
            }
        }
    }

    private void OnScrollToBottomClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (messageScrollViewer == null)
            return;

        messageScrollViewer.ScrollToEnd();
        isNearBottom = true;

        if (scrollToBottomButton != null)
        {
            scrollToBottomButton.IsVisible = false;
        }
    }

    /// <summary>
    /// Checks if the drag data contains valid image files
    /// </summary>
    private bool ContainsValidImageFiles(DragEventArgs e)
    {
        if (e.Data.Get(DataFormats.Files) is not IEnumerable<IStorageItem> files)
            return false;

        var paths = files.Select(f => f.Path.LocalPath).ToList();
        return paths.Count > 0 && paths.All(p => SupportedImageExtensions.Contains(Path.GetExtension(p)));
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (!ContainsValidImageFiles(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;

        if (DataContext is BananaVisionPageViewModel viewModel)
        {
            viewModel.IsDragOverImage = true;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is BananaVisionPageViewModel viewModel)
        {
            viewModel.IsDragOverImage = false;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!ContainsValidImageFiles(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is BananaVisionPageViewModel viewModel)
        {
            viewModel.IsDragOverImage = false;
        }

        if (e.Data.Get(DataFormats.Files) is not IEnumerable<IStorageItem> files)
            return;

        var imagePaths = files
            .Select(f => f.Path.LocalPath)
            .Where(p => SupportedImageExtensions.Contains(Path.GetExtension(p)))
            .ToList();

        if (imagePaths.Count == 0)
            return;

        e.Handled = true;

        if (DataContext is BananaVisionPageViewModel viewModel2)
        {
            viewModel2.AddImagesFromPaths(imagePaths);
        }
    }

    /// <summary>
    /// Handles keyboard events for paste (Ctrl+V)
    /// </summary>
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Check for Ctrl+V (paste)
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(PlatformKeyModifiers.CommandModifier))
        {
            if (DataContext is BananaVisionPageViewModel viewModel)
            {
                var handled = await viewModel.TryPasteImagesFromClipboardAsync();
                if (handled)
                {
                    e.Handled = true;
                }
            }
        }
    }
}
