using System;
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
using Microsoft.Extensions.DependencyInjection;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Controls.Dock;

/// <summary>
/// Base for Dock controls
/// Expects a <see cref="DockControl"/> named "Dock" in the XAML
/// </summary>
public abstract class DockUserControlBase : DropTargetUserControlBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private DockControl? baseDock;
    private readonly DockSerializer dockSerializer = new(typeof(AvaloniaList<>));
    private readonly DockState dockState = new();
    private bool hasAppliedPersistedLayout;

    /// <summary>
    /// Resolved once while the view is attached. Layout persistence also runs during app
    /// shutdown, inside the service provider's own dispose, where resolving services throws
    /// ObjectDisposedException - so it must never be resolved at persist time.
    /// </summary>
    private ISettingsManager? settingsManager;

    /// <summary>
    /// Serialized snapshot of the XAML-defined layout, captured before any persisted layout is
    /// applied. Restore-default must go through a snapshot: the layout object tree is mutated
    /// in place by user drag operations, so holding a reference to it cannot restore anything.
    /// </summary>
    private string? defaultLayoutJson;

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
            defaultLayoutJson ??= dockSerializer.Serialize(layout);

            if (!hasAppliedPersistedLayout)
            {
                hasAppliedPersistedLayout = true;
                ApplyPersistedLayout();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!Design.IsDesignMode)
        {
            settingsManager ??= App.Services.GetRequiredService<ISettingsManager>();
        }

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

        PersistDefaultTypeLayout();

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
            TryLoadDockLayout(layout);
        }
        else
        {
            // Restore default
            RestoreDefaultDockLayout();
        }
    }

    /// <summary>
    /// Applies the persisted layout for this tab when it first loads: a layout carried by the
    /// opened project file wins over the remembered per-project-type layout; the XAML default
    /// remains when neither exists or fails to apply.
    /// </summary>
    private void ApplyPersistedLayout()
    {
        if (Design.IsDesignMode)
            return;

        if (DataContext is InferenceTabViewModelBase { PendingViewState.DockLayout: { } projectLayout } vm)
        {
            vm.PendingViewState = null;

            if (TryLoadDockLayout(projectLayout))
                return;
        }

        if (GetLayoutKey() is { } key && settingsManager is not null)
        {
            if (settingsManager.Settings.InferenceDockLayouts.GetValueOrDefault(key) is { } layoutJson)
            {
                TryLoadDockLayout(layoutJson);
            }
        }
    }

    /// <summary>
    /// Remembers the current layout as the default for this tab's project type,
    /// so new tabs of the same type open with it.
    /// </summary>
    internal void PersistDefaultTypeLayout()
    {
        if (Design.IsDesignMode || baseDock?.Layout is null || settingsManager is null)
            return;

        if (GetLayoutKey() is not { } key)
            return;

        try
        {
            if (SaveDockLayout() is not { } layoutJson)
                return;

            if (settingsManager.Settings.InferenceDockLayouts.GetValueOrDefault(key) == layoutJson)
                return;

            settingsManager.Transaction(s => s.InferenceDockLayouts[key] = layoutJson);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to persist default dock layout for {Key}", key);
        }
    }

    /// <summary>
    /// Restores the XAML-defined layout and forgets the remembered per-type layout.
    /// </summary>
    private void RestoreDefaultDockLayout()
    {
        if (defaultLayoutJson is null || !TryLoadDockLayout(defaultLayoutJson))
            return;

        if (Design.IsDesignMode || GetLayoutKey() is not { } key || settingsManager is null)
            return;

        try
        {
            if (settingsManager.Settings.InferenceDockLayouts.ContainsKey(key))
            {
                settingsManager.Transaction(s => s.InferenceDockLayouts.Remove(key));
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to clear default dock layout for {Key}", key);
        }
    }

    private bool TryLoadDockLayout(string data)
    {
        if (baseDock?.Layout is not { } currentLayout)
            return false;

        try
        {
            if (dockSerializer.Deserialize<IDock?>(data) is not { } layout)
                return false;

            if (!DockLayoutGrafter.TryGraftContent(layout, currentLayout))
            {
                Logger.Info("Saved dock layout does not match current panes, keeping current layout");
                return false;
            }

            baseDock.Layout = layout;
            dockState.Restore(layout);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to load dock layout");
            return false;
        }
    }

    protected string? SaveDockLayout()
    {
        return baseDock is null ? null : dockSerializer.Serialize(baseDock.Layout);
    }

    /// <summary>
    /// Settings key for the per-project-type layout, or null if the tab type is unmapped.
    /// </summary>
    private string? GetLayoutKey()
    {
        if (DataContext is not IJsonLoadableState loadable)
            return null;

        return loadable.ToProjectType() is var type && type != InferenceProjectType.Unknown
            ? type.ToString()
            : null;
    }
}
