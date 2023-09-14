using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Avalonia.Json;
using Dock.Model.Core;

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

    protected virtual void LoadDockLayout(string data)
    {
        if (BaseDock is null)
            return;

        if (DockSerializer.Deserialize<IDock?>(data) is { } layout)
        {
            BaseDock.Layout = layout;
            DockState.Restore(layout);
        }
    }

    protected virtual string SaveDockLayout()
    {
        return BaseDock is null ? string.Empty : DockSerializer.Serialize(BaseDock.Layout);
    }
}
