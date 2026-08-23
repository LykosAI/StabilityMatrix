using System.Collections.Generic;
using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace StabilityMatrix.Avalonia.Controls.Dock;

/// <summary>
/// Rebinds live content-carrying dockables (tools/documents) into a deserialized dock layout.
/// </summary>
/// <remarks>
/// Dock's serializer excludes <c>Content</c>, so a deserialized layout is a structure-only
/// skeleton with empty panes. Restoring a layout therefore takes the saved skeleton (structure,
/// proportions, pinned state) and swaps each of its tools for the live instance with the same
/// <see cref="IDockable.Id"/>, which still carries the XAML-defined content. If the saved and
/// live layouts do not contain exactly the same tool Ids (e.g. the saved layout came from an
/// app version with different panes), the graft is rejected and the current layout should be
/// kept, so stale layouts self-heal across app updates instead of hiding new panes.
/// </remarks>
public static class DockLayoutGrafter
{
    /// <summary>
    /// Attempts to swap all content dockables in <paramref name="skeleton"/> for the live
    /// instances in <paramref name="liveLayout"/>, matched by Id.
    /// </summary>
    /// <returns>
    /// True if the graft succeeded and <paramref name="skeleton"/> is safe to display.
    /// False if the layouts' tool Ids don't match exactly, or Ids are missing/ambiguous;
    /// the skeleton must not be used in that case.
    /// </returns>
    public static bool TryGraftContent(IDock skeleton, IDock liveLayout)
    {
        var liveById = new Dictionary<string, IDockable>();

        foreach (var dockable in EnumerateDockables(liveLayout).Where(IsContentDockable))
        {
            if (string.IsNullOrEmpty(dockable.Id))
                return false;

            // The same instance can legitimately appear in multiple collections
            // (e.g. visible + pinned); two distinct instances sharing an Id cannot be matched.
            if (liveById.TryGetValue(dockable.Id, out var existing))
            {
                if (!ReferenceEquals(existing, dockable))
                    return false;
            }
            else
            {
                liveById[dockable.Id] = dockable;
            }
        }

        var slots = new List<(IList<IDockable> List, int Index, string Id)>();
        var skeletonById = new Dictionary<string, IDockable>();

        foreach (var (list, index) in EnumerateSlots(skeleton))
        {
            var dockable = list[index];
            if (!IsContentDockable(dockable))
                continue;

            if (string.IsNullOrEmpty(dockable.Id))
                return false;

            if (skeletonById.TryGetValue(dockable.Id, out var existing))
            {
                if (!ReferenceEquals(existing, dockable))
                    return false;
            }
            else
            {
                skeletonById[dockable.Id] = dockable;
            }

            slots.Add((list, index, dockable.Id));
        }

        if (!skeletonById.Keys.ToHashSet().SetEquals(liveById.Keys))
            return false;

        foreach (var (list, index, id) in slots)
        {
            list[index] = liveById[id];
        }

        // Dock-level references may still point at the orphaned skeleton stand-ins
        foreach (var dockable in EnumerateDockables(skeleton))
        {
            if (dockable is not IDock dock)
                continue;

            dock.ActiveDockable = Remap(dock.ActiveDockable);
            dock.DefaultDockable = Remap(dock.DefaultDockable);
            dock.FocusedDockable = Remap(dock.FocusedDockable);
        }

        return true;

        IDockable? Remap(IDockable? dockable) =>
            dockable is not null && IsContentDockable(dockable) && dockable.Id is { Length: > 0 } id
                ? liveById.GetValueOrDefault(id, dockable)
                : dockable;
    }

    /// <summary>
    /// A dockable whose content cannot be serialized and must come from the live layout.
    /// Structural nodes (docks, splitters) deserialize fully and are kept from the skeleton.
    /// </summary>
    private static bool IsContentDockable(IDockable dockable) => dockable is ITool or IDocument;

    private static IEnumerable<IDockable> EnumerateDockables(IDockable root)
    {
        yield return root;

        foreach (var (list, index) in EnumerateSlots(root))
        {
            yield return list[index];
        }
    }

    /// <summary>
    /// Enumerates every (list, index) slot holding a dockable reachable from
    /// <paramref name="root"/>: visible children, and for root docks also hidden and pinned
    /// dockables plus floating window layouts.
    /// </summary>
    private static IEnumerable<(IList<IDockable> List, int Index)> EnumerateSlots(IDockable root)
    {
        var pending = new Stack<IDockable>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (current is IRootDock rootDock)
            {
                foreach (var window in rootDock.Windows ?? [])
                {
                    if (window.Layout is { } windowLayout)
                    {
                        pending.Push(windowLayout);
                    }
                }
            }

            foreach (var list in ListsOf(current))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    yield return (list, i);
                    pending.Push(list[i]);
                }
            }
        }
    }

    private static IEnumerable<IList<IDockable>> ListsOf(IDockable dockable)
    {
        if (dockable is IDock { VisibleDockables: { } visible })
            yield return visible;

        if (dockable is IRootDock rootDock)
        {
            if (rootDock.HiddenDockables is { } hidden)
                yield return hidden;
            if (rootDock.LeftPinnedDockables is { } left)
                yield return left;
            if (rootDock.RightPinnedDockables is { } right)
                yield return right;
            if (rootDock.TopPinnedDockables is { } top)
                yield return top;
            if (rootDock.BottomPinnedDockables is { } bottom)
                yield return bottom;
        }
    }
}
