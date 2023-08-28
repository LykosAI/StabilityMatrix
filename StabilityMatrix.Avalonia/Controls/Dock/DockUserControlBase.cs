using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Avalonia.Json;
using Dock.Model.Core;

namespace StabilityMatrix.Avalonia.Controls.Dock;

/// <summary>
/// Base for Dock controls
/// Expects a <see cref="DockControl"/> named "Dock" in the XAML
/// </summary>
public abstract class DockUserControlBase : UserControlBase
{
    private DockControl _dock = null!;
    protected readonly AvaloniaDockSerializer DockSerializer = new();
    protected readonly DockState DockState = new();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        _dock = this.FindControl<DockControl>("Dock") 
                ?? throw new NullReferenceException("DockControl not found");
        
        if (_dock.Layout is { } layout)
        {
            DockState.Save(layout);
        }
    }

    protected virtual void LoadDockLayout(string data)
    {
        if (DockSerializer.Deserialize<IDock?>(data) is { } layout)
        {
            _dock.Layout = layout;
            DockState.Restore(layout);
        }
    }
    
    protected virtual string SaveDockLayout()
    {
        return DockSerializer.Serialize(_dock.Layout);
    }
}
