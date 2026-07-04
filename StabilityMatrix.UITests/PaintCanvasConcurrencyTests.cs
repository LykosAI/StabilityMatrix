using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.ViewModels.Controls;

namespace StabilityMatrix.UITests;

/// <summary>
/// Phase 0 stress scaffold for the races that the threading redesign will fix. These tests are
/// implemented fully but SKIPPED so CI stays green while the current code still races. They are
/// intended to be enabled in Phase 5 (after the redesign) to prove the races are gone.
///
/// One-time characterization (Phase 0, run manually with the Skip removed, GPU disabled / CPU-only
/// headless Skia) against the CURRENT code — 3 runs each:
///   * Test A (live-stroke append vs ToSKPath/index reads): PASSED all 3 runs, no throw. The writer
///     completes 100k appends almost immediately (~11ms total), so in this configuration the reader
///     rarely overlaps a backing-array resize. The race is real in principle (List&lt;PenPoint&gt; is
///     not thread-safe and ToSKPath/RenderFreehandPath only snapshot Count, not the array reference),
///     but this harness does NOT reliably reproduce it. Phase 5 should consider a slower/paced writer
///     or a longer-lived overlap to actually surface it.
///   * Test B (export vs mutate): PASSED all 3 runs (full 2s window), no throw, no AccessViolation,
///     host exit code 0. The headless export path composites onto CPU surfaces guarded by renderLock
///     + per-layer locks, which is sufficient here. The AccessViolation-class crashes the VM comments
///     describe happen on the GPU compositor render thread (on-screen leased GPU surfaces tied to that
///     thread) — a path this off-screen export harness does not exercise. Phase 5 may need an
///     on-screen / GPU-backed scenario to characterize the real crash.
/// </summary>
public class PaintCanvasConcurrencyTests
{
    private const string SkipReason = "Enabled in Phase 5 after threading redesign";

    // ==== Test A: live stroke append vs concurrent path reads ====

    [AvaloniaFact(Skip = SkipReason)]
    public async Task LiveStroke_AppendWhileReading_DoesNotThrow()
    {
        await RunLiveStrokeStress();
    }

    /// <summary>
    /// Core body split out so the one-time characterization run can call it directly (temporarily
    /// removing the Skip on the fact above, or invoking this from a scratch harness).
    /// </summary>
    private static async Task RunLiveStrokeStress()
    {
        var penPath = new PenPath
        {
            FillColor = SKColors.Red,
            Radius = 3f,
            PathType = PenPathType.Freehand,
        };

        using var cts = new CancellationTokenSource();
        Exception? failure = null;

        // Writer: append points in a tight loop (~100k).
        var writer = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 100_000; i++)
                {
                    penPath.Points.Add(
                        new PenPoint((ulong)(i % 64), (ulong)((i / 64) % 64)) { IsPen = true }
                    );
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                cts.Cancel();
            }
        });

        // Reader: convert to SKPath and simulate RenderFreehandPath-style index reads.
        var reader = Task.Run(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    using var skPath = penPath.ToSKPath();

                    var points = penPath.Points;
                    var count = points.Count;
                    for (var i = 0; i < count; i++)
                    {
                        // Mirror RenderFreehandPath: read fields by index up to the snapshotted count.
                        var p = points[i];
                        _ = p.X + p.Y + (p.Pressure ?? 1) + (p.IsPen ? 1 : 0);
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        await Task.WhenAll(writer, reader);
        Assert.Null(failure);
    }

    // ==== Test B: export vs mutate ====

    [AvaloniaFact(Skip = SkipReason)]
    public async Task Export_WhileMutating_DoesNotThrow()
    {
        await RunExportMutateStress(TimeSpan.FromSeconds(2));
    }

    private static async Task RunExportMutateStress(TimeSpan duration)
    {
        var vm = TestHelpers.CreatePaintCanvasViewModel();
        vm.CanvasSize = new System.Drawing.Size(64, 64);
        vm.Paths = ImmutableList.Create(
            TestHelpers.BuildPenStroke(SKColors.Red),
            TestHelpers.BuildMouseStroke(SKColors.Blue),
            TestHelpers.BuildRectangle(SKColors.Green, new SKRect(20, 20, 50, 50))
        );

        using var cts = new CancellationTokenSource(duration);
        var token = cts.Token;
        var failures = new List<Exception>();
        var failuresLock = new object();

        void Record(Exception ex)
        {
            lock (failuresLock)
            {
                failures.Add(ex);
            }
        }

        // (a) RenderToImage in a loop.
        var renderTask = Task.Run(() =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using var image = vm.RenderToImage();
                }
            }
            catch (Exception ex)
            {
                Record(ex);
            }
        });

        // (b) RenderToSurface onto a locally created CPU surface.
        var surfaceTask = Task.Run(() =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using var surface = SKSurface.Create(new SKImageInfo(64, 64));
                    if (surface is not null)
                    {
                        vm.RenderToSurface(surface);
                    }
                }
            }
            catch (Exception ex)
            {
                Record(ex);
            }
        });

        // (c) Undo / Redo / ClearCanvas / SetLayerBitmap with small bitmaps.
        var mutateTask = Task.Run(() =>
        {
            try
            {
                var i = 0;
                while (!token.IsCancellationRequested)
                {
                    switch (i++ % 5)
                    {
                        case 0:
                            vm.Paths = vm.Paths.Add(TestHelpers.BuildPenStroke(SKColors.Yellow));
                            break;
                        case 1:
                            vm.Undo();
                            break;
                        case 2:
                            vm.Redo();
                            break;
                        case 3:
                            using (var bmp = new SKBitmap(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul))
                            {
                                // Give SetLayerBitmap ownership of a fresh copy each time.
                                vm.SetLayerBitmap("Images", bmp.Copy());
                            }
                            break;
                        case 4:
                            vm.ClearCanvas();
                            vm.Paths = ImmutableList.Create(
                                TestHelpers.BuildRectangle(SKColors.Green, new SKRect(10, 10, 40, 40))
                            );
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Record(ex);
            }
        });

        await Task.WhenAll(renderTask, surfaceTask, mutateTask);

        vm.Dispose();

        Assert.Empty(failures);
    }
}
