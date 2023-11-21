using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Attributes;
using CheckpointFolder = StabilityMatrix.Avalonia.ViewModels.CheckpointManager.CheckpointFolder;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class CheckpointsPage : UserControlBase
{
    private ItemsControl? repeater;
    private IDisposable? subscription;

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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        subscription?.Dispose();
        subscription = null;

        if (DataContext is CheckpointsPageViewModel vm)
        {
            subscription = vm.WhenPropertyChanged(m => m.ShowConnectedModelImages)
                .Subscribe(_ => InvalidateRepeater());
        }
    }

    private void InvalidateRepeater()
    {
        repeater ??= this.FindControl<ItemsControl>("FilesRepeater");
        repeater?.InvalidateArrange();
        repeater?.InvalidateMeasure();

        foreach (var child in this.GetVisualDescendants().OfType<ItemsRepeater>())
        {
            child?.InvalidateArrange();
            child?.InvalidateMeasure();
        }
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
            folder.IsExpanded = true;
            folder.IsCurrentDragTarget = true;
        }
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is CheckpointsPageViewModel vm)
        {
            vm.ClearSearchQuery();
        }
    }
}
