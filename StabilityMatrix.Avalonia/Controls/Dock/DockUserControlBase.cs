using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Core;
using Dock.Serializer;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Controls.Dock;

/// <summary>
/// Base for Dock controls
/// Expects a <see cref="DockControl"/> named "Dock" in the XAML
/// </summary>
public abstract class DockUserControlBase : DropTargetUserControlBase
{
    private DockControl? baseDock;
    private readonly DockSerializer dockSerializer = new(typeof(AvaloniaList<>));
    private readonly DockState dockState = new();
    private readonly DockState initialDockState = new();
    private IDock? initialLayout;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        baseDock =
            this.FindControl<DockControl>("Dock")
            ?? throw new NullReferenceException("DockControl not found");

        if (baseDock.Layout is { } layout)
        {
            dockState.Save(layout);
            initialDockState.Save(layout);
            initialLayout = layout;
            // Dispatcher.UIThread.Post(() => dockState.Save(layout), DispatcherPriority.Background);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Attach handlers for view state saving and loading
        if (DataContext is InferenceTabViewModelBase vm)
        {
            vm.SaveViewStateRequested += DataContext_OnSaveViewStateRequested;
            vm.LoadViewStateRequested += DataContext_OnLoadViewStateRequested;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Detach handlers for view state saving and loading
        if (DataContext is InferenceTabViewModelBase vm)
        {
            vm.SaveViewStateRequested -= DataContext_OnSaveViewStateRequested;
            vm.LoadViewStateRequested -= DataContext_OnLoadViewStateRequested;
        }
    }

    private void DataContext_OnSaveViewStateRequested(object? sender, SaveViewStateEventArgs args)
    {
        var saveTcs = new TaskCompletionSource<ViewState>();

        Dispatcher.UIThread.Post(() =>
        {
            var state = new ViewState { DockLayout = SaveDockLayout() };
            saveTcs.SetResult(state);
        });

        args.StateTask ??= saveTcs.Task;
    }

    private void DataContext_OnLoadViewStateRequested(object? sender, LoadViewStateEventArgs args)
    {
        if (args.State?.DockLayout is { } layout)
        {
            // Provided
            LoadDockLayout(layout);
        }
        else
        {
            // Restore default
            RestoreDockLayout();
        }
    }

    private void LoadDockLayout(JsonObject data)
    {
        LoadDockLayout(data.ToJsonString());
    }

    private void LoadDockLayout(string data)
    {
        if (baseDock is null)
            return;

        if (dockSerializer.Deserialize<IDock?>(data) is { } layout)
        {
            baseDock.Layout = layout;
            dockState.Restore(baseDock.Layout);
        }
    }

    private void RestoreDockLayout()
    {
        if (baseDock != null && initialLayout != null)
        {
            baseDock.Layout = initialLayout;
            initialDockState.Restore(baseDock.Layout);
        }
    }

    protected string? SaveDockLayout()
    {
        return baseDock is null ? null : dockSerializer.Serialize(baseDock.Layout);
    }
}
