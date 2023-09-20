using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Avalonia.Json;
using Dock.Model.Core;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Controls.Dock;

/// <summary>
/// Base for Dock controls
/// Expects a <see cref="DockControl"/> named "Dock" in the XAML
/// </summary>
public abstract class DockUserControlBase : DropTargetUserControlBase
{
    protected DockControl? BaseDock;
    protected readonly AvaloniaDockSerializer DockSerializer = new();
    protected readonly DockState DockState = new();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        BaseDock =
            this.FindControl<DockControl>("Dock")
            ?? throw new NullReferenceException("DockControl not found");

        if (BaseDock.Layout is { } layout)
        {
            Dispatcher.UIThread.Post(() => DockState.Save(layout), DispatcherPriority.Background);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Attach handlers for view state saving and loading
        if (DataContext is InferenceTabViewModelBase vm)
        {
            vm.SaveViewStateRequested += (_, args) =>
            {
                var saveTcs = new TaskCompletionSource<ViewState>();

                Dispatcher.UIThread.Post(() =>
                {
                    var state = new ViewState { DockLayout = SaveDockLayout() };
                    saveTcs.SetResult(state);
                });

                args.StateTask ??= saveTcs.Task;
            };

            vm.LoadViewStateRequested += (_, args) =>
            {
                if (args.State.DockLayout is { } layout)
                {
                    LoadDockLayout(layout);
                }
            };
        }
    }

    protected void LoadDockLayout(JsonObject data)
    {
        LoadDockLayout(data.ToJsonString());
    }

    protected void LoadDockLayout(string data)
    {
        if (BaseDock is null)
            return;

        if (DockSerializer.Deserialize<IDock?>(data) is { } layout)
        {
            BaseDock.Layout = layout;
            DockState.Restore(layout);
        }
    }

    protected string? SaveDockLayout()
    {
        return BaseDock is null ? null : DockSerializer.Serialize(BaseDock.Layout);
    }
}
