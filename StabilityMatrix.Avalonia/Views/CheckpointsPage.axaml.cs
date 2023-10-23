using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using CheckpointFolder = StabilityMatrix.Avalonia.ViewModels.CheckpointManager.CheckpointFolder;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class CheckpointsPage : UserControlBase
{
    private ItemsControl? repeater;

    public CheckpointsPage()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragExit);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        EventManager.Instance.InvalidateRepeaterRequested += OnInvalidateRepeaterRequested;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        EventManager.Instance.InvalidateRepeaterRequested -= OnInvalidateRepeaterRequested;
    }

    private void OnInvalidateRepeaterRequested(object? sender, EventArgs e)
    {
        var sw = Stopwatch.StartNew();
        repeater ??= this.FindControl<ItemsControl>("FilesRepeater");
        repeater?.InvalidateArrange();
        repeater?.InvalidateMeasure();

        foreach (var child in this.GetVisualDescendants().OfType<ItemsRepeater>())
        {
            child?.InvalidateArrange();
            child?.InvalidateMeasure();
        }

        sw.Stop();
        Debug.WriteLine($"InvalidateRepeaterRequested took {sw.Elapsed.TotalMilliseconds}ms");
    }

    private static async void OnDrop(object? sender, DragEventArgs e)
    {
        var sourceDataContext = (e.Source as Control)?.DataContext;
        if (sourceDataContext is CheckpointFolder folder)
        {
            await folder.OnDrop(e);
        }
    }

    private static void OnDragExit(object? sender, DragEventArgs e)
    {
        var sourceDataContext = (e.Source as Control)?.DataContext;
        if (sourceDataContext is CheckpointFolder folder)
        {
            folder.IsCurrentDragTarget = false;
        }
    }

    private static void OnDragEnter(object? sender, DragEventArgs e)
    {
        // Only allow Copy or Link as Drop Operations.
        e.DragEffects &= DragDropEffects.Copy | DragDropEffects.Link;

        // Only allow if the dragged data contains text or filenames.
        if (!e.Data.Contains(DataFormats.Text) && !e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.None;
        }

        // Forward to view model
        var sourceDataContext = (e.Source as Control)?.DataContext;
        if (sourceDataContext is CheckpointFolder folder)
        {
            folder.IsCurrentDragTarget = true;
        }
    }
}
