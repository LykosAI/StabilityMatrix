using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls.Scroll;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class CheckpointsPage : ResizableUserControlBase
{
    private Dictionary<CheckpointCategory, DispatcherTimer> dragTimers = new();

    public CheckpointsPage()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragExit);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override Action OnResizeFactorChanged =>
        () =>
        {
            ImageRepeater.InvalidateMeasure();
            ImageRepeater.InvalidateArrange();
        };

    protected override double MinResizeFactor => 0.6d;
    protected override double MaxResizeFactor => 1.5d;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Get(DataFormats.Files) is not IEnumerable<IStorageItem> files)
            return;

        var paths = files.Select(f => f.Path.LocalPath);
        if (paths.All(p => LocalModelFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(p))))
            return;

        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragExit(object? sender, DragEventArgs e)
    {
        if (e.Source is not Control control)
            return;

        if (DataContext as CheckpointsPageViewModel is not { SelectedCategory: not null } checkpointsVm)
            return;

        switch (control)
        {
            case TreeViewItem treeViewItem:
                treeViewItem.Classes.Remove("dragover");
                break;
            case Border { Tag: "DragOverlay" }:
                checkpointsVm.IsDragOver = false;
                break;
            case Border border:
                border.Classes.Remove("dragover");
                break;
            case TextBlock textBlock:
                textBlock.Parent?.Classes.Remove("dragover");
                break;
        }

        var sourceDataContext = control switch
        {
            TreeViewItem treeView => treeView.DataContext,
            Border border => border.Parent?.Parent?.DataContext,
            TextBlock textBlock => textBlock.Parent?.DataContext,
            _ => null
        };

        if (sourceDataContext is not CheckpointCategory category)
            return;

        if (!dragTimers.TryGetValue(category, out var timer))
            return;

        timer.Stop();
        dragTimers.Remove(category);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Source is not Control control)
            return;

        if (DataContext as CheckpointsPageViewModel is not { SelectedCategory: not null } checkpointsVm)
            return;

        if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
        {
            var paths = files.Select(f => f.Path.LocalPath);
            if (paths.Any(p => !LocalModelFile.SupportedCheckpointExtensions.Contains(Path.GetExtension(p))))
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
        }

        switch (control)
        {
            case TreeViewItem treeViewItem:
                treeViewItem.Classes.Add("dragover");
                break;
            case Border border:
                border.Classes.Add("dragover");
                break;
            case TextBlock textBlock:
                textBlock.Parent?.Classes.Add("dragover");
                break;
            case BetterScrollContentPresenter _:
                checkpointsVm.IsDragOver = true;
                break;
        }

        var sourceDataContext = control switch
        {
            TreeViewItem treeView => treeView.DataContext,
            Border border => border.Parent?.Parent?.DataContext,
            TextBlock textBlock => textBlock.Parent?.DataContext,
            _ => null
        };

        if (sourceDataContext is not CheckpointCategory category)
            return;

        if (dragTimers.TryGetValue(category, out var timer))
        {
            timer.Stop();
            timer.Start();
        }
        else
        {
            var newTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
                IsEnabled = true
            };
            newTimer.Tick += (timerObj, _) =>
            {
                var treeViewItem = control switch
                {
                    TreeViewItem treeView => treeView,
                    Border border => border.Parent?.Parent as TreeViewItem,
                    TextBlock textBlock => textBlock.Parent as TreeViewItem,
                    _ => null
                };

                if (treeViewItem != null && category.SubDirectories.Count > 0)
                {
                    treeViewItem.IsExpanded = true;
                }

                (timerObj as DispatcherTimer)?.Stop();
                dragTimers.Remove(category);
            };
            dragTimers.Add(category, newTimer);
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var control = e.Source as Control;
        var sourceDataContext = control?.DataContext;
        if (DataContext as CheckpointsPageViewModel is not { SelectedCategory: not null } checkpointsVm)
        {
            return;
        }

        switch (control)
        {
            case TreeViewItem treeViewItem:
                treeViewItem.Classes.Remove("dragover");
                break;
            case Border border:
                border.Classes.Remove("dragover");
                break;
            case TextBlock textBlock:
                textBlock.Parent?.Classes.Remove("dragover");
                break;
        }

        checkpointsVm.IsDragOver = false;

        switch (sourceDataContext)
        {
            case CheckpointCategory category:
                if (e.Data.GetContext<CheckpointFileViewModel>() is { } vm)
                {
                    var allSelectedCheckpoints = checkpointsVm.Models.Where(x => x.IsSelected).ToList();

                    if (allSelectedCheckpoints.Any() && checkpointsVm.DragMovesAllSelected)
                    {
                        if (!allSelectedCheckpoints.Contains(vm))
                        {
                            allSelectedCheckpoints.Add(vm);
                        }

                        foreach (var checkpoint in allSelectedCheckpoints)
                        {
                            await checkpointsVm.MoveBetweenFolders(checkpoint.CheckpointFile, category.Path);
                        }
                    }
                    else
                    {
                        await checkpointsVm.MoveBetweenFolders(vm.CheckpointFile, category.Path);
                    }
                }
                else if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
                {
                    var paths = files.Select(f => f.Path.LocalPath).ToArray();
                    await checkpointsVm.ImportFilesAsync(paths, category.Path);
                }
                break;
            case CheckpointsPageViewModel _:
            case CheckpointFileViewModel _:
                if (e.Data.GetContext<CheckpointFileViewModel>() is { } fileVm)
                {
                    await checkpointsVm.MoveBetweenFolders(
                        fileVm.CheckpointFile,
                        checkpointsVm.SelectedCategory.Path
                    );
                }
                else if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
                {
                    var paths = files.Select(f => f.Path.LocalPath).ToArray();
                    await checkpointsVm.ImportFilesAsync(paths, checkpointsVm.SelectedCategory.Path);
                }
                break;
        }
    }
}
