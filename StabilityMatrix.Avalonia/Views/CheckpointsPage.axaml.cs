using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.FileInterfaces;
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
            subscription = vm.WhenPropertyChanged(m => m.ShowConnectedModelImages).Subscribe(_ => InvalidateRepeater());
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

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var sourceDataContext = (e.Source as Control)?.DataContext;
        switch (sourceDataContext)
        {
            case CheckpointFolder folder:
            {
                if (e.Data.Get("Context") is not CheckpointFile file)
                {
                    await folder.OnDrop(e);
                    break;
                }

                var filePath = new FilePath(file.FilePath);
                if (filePath.Directory?.FullPath != folder.DirectoryPath)
                {
                    await folder.OnDrop(e);
                }
                break;
            }
            case CheckpointFile file:
            {
                if (e.Data.Get("Context") is not CheckpointFile dragFile)
                {
                    await file.ParentFolder.OnDrop(e);
                    break;
                }

                var parentFolder = file.ParentFolder;
                var dragFilePath = new FilePath(dragFile.FilePath);
                if (dragFilePath.Directory?.FullPath != parentFolder.DirectoryPath)
                {
                    await parentFolder.OnDrop(e);
                }
                break;
            }
        }
    }

    private static void OnDragExit(object? sender, DragEventArgs e)
    {
        var sourceDataContext = (e.Source as Control)?.DataContext;
        switch (sourceDataContext)
        {
            case CheckpointFolder folder:
                folder.IsCurrentDragTarget = false;
                break;
            case CheckpointFile file:
                file.ParentFolder.IsCurrentDragTarget = false;
                break;
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
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
        switch (sourceDataContext)
        {
            case CheckpointFolder folder:
            {
                folder.IsExpanded = true;
                if (e.Data.Get("Context") is not CheckpointFile file)
                {
                    folder.IsCurrentDragTarget = true;
                    break;
                }

                var filePath = new FilePath(file.FilePath);
                folder.IsCurrentDragTarget = filePath.Directory?.FullPath != folder.DirectoryPath;
                break;
            }
            case CheckpointFile file:
            {
                if (e.Data.Get("Context") is not CheckpointFile dragFile)
                {
                    file.ParentFolder.IsCurrentDragTarget = true;
                    break;
                }

                var parentFolder = file.ParentFolder;
                var dragFilePath = new FilePath(dragFile.FilePath);
                parentFolder.IsCurrentDragTarget = dragFilePath.Directory?.FullPath != parentFolder.DirectoryPath;
                break;
            }
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
