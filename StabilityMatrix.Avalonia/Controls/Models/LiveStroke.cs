using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace StabilityMatrix.Avalonia.Controls.Models;

/// <summary>
/// An in-progress stroke being drawn by the user. Unlike a finalized <see cref="PenPath"/>,
/// a live stroke is written by the UI thread (pointer events appending points) while the
/// compositor render thread concurrently reads it every frame.
/// </summary>
/// <remarks>
/// Thread safety comes from copy-on-append publication rather than locks: points live in an
/// array that is never mutated after it becomes visible to readers. Appending copies the
/// current array into a larger one and publishes it with a single
/// <see cref="Volatile.Write{T}(ref T, T)"/>, so a reader always observes a fully written,
/// stable snapshot — there is no window where a resize or in-place write can tear a read.
/// Writes must come from a single thread (the UI thread); reads may come from any thread.
/// </remarks>
public sealed class LiveStroke
{
    private PenPoint[] points = [];

    /// <summary>
    /// Immutable stroke metadata (color, radius, feathering, shape bounds, etc.).
    /// The template's own <see cref="PenPath.Points"/> list is unused and stays empty;
    /// live points are tracked by this class instead.
    /// </summary>
    public required PenPath Template { get; init; }

    /// <summary>
    /// Returns a stable snapshot of the points recorded so far. The returned array is
    /// never mutated — callers may iterate it freely on any thread.
    /// </summary>
    public PenPoint[] GetPointsSnapshot() => Volatile.Read(ref points);

    /// <summary>
    /// Appends points to the stroke. Must only be called from the single writer (UI) thread.
    /// </summary>
    public void AddPoints(ReadOnlySpan<PenPoint> newPoints)
    {
        if (newPoints.IsEmpty)
            return;

        // Single-writer: no other thread mutates `points`, so a plain read is sufficient here.
        var current = points;
        var next = new PenPoint[current.Length + newPoints.Length];
        current.AsSpan().CopyTo(next);
        newPoints.CopyTo(next.AsSpan(current.Length));

        // Release-publish: the array contents above are guaranteed visible to any thread
        // that observes the new reference.
        Volatile.Write(ref points, next);
    }

    /// <summary>
    /// Snapshots this live stroke into a finalized, fully independent <see cref="PenPath"/>.
    /// </summary>
    public PenPath ToPenPath() => Template with { Points = [.. GetPointsSnapshot()] };

    /// <summary>
    /// Creates a live stroke from a previously serialized <see cref="PenPath"/>.
    /// </summary>
    public static LiveStroke FromPenPath(PenPath path)
    {
        var stroke = new LiveStroke { Template = path with { Points = [] } };
        stroke.AddPoints(CollectionsMarshal.AsSpan(path.Points));
        return stroke;
    }
}
