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
    // Both stress tests are live: Test A was enabled in Phase 3 (LiveStroke), Test B in Phase 5
    // (lock-free ownership model). The characterization notes in the class doc describe how the
    // PRE-redesign code behaved when these were first written.

    // ==== Test A: live stroke append vs concurrent snapshot reads ====
    // Enabled as of Phase 3: LiveStroke publishes copy-on-append snapshots, so concurrent
    // append-while-render is structurally safe (a captured array is never mutated).

    [AvaloniaFact]
    public async Task LiveStroke_AppendWhileReading_DoesNotThrow()
    {
        await RunLiveStrokeStress();
    }

    private static async Task RunLiveStrokeStress()
    {
        var stroke = new LiveStroke
        {
            Template = new PenPath
            {
                FillColor = SKColors.Red,
                Radius = 3f,
                PathType = PenPathType.Freehand,
            },
        };

        using var cts = new CancellationTokenSource();
        Exception? failure = null;

        // Writer: publish points in small batches (mirrors HandlePointerMoved's per-event batch),
        // yielding occasionally so the reader gets real overlap with array growth.
        var writer = Task.Run(() =>
        {
            try
            {
                var batch = new PenPoint[8];
                for (var i = 0; i < 100_000; i += batch.Length)
                {
                    for (var j = 0; j < batch.Length; j++)
                    {
                        var n = i + j;
                        batch[j] = new PenPoint((ulong)(n % 64), (ulong)((n / 64) % 64))
                        {
                            IsPen = true,
                            Pressure = (n % 100) / 100.0,
                        };
                    }

                    stroke.AddPoints(batch);

                    if (i % 1024 == 0)
                    {
                        Thread.Yield();
                    }
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

        // Reader: render snapshots to a CPU canvas and verify prefix consistency — the number of
        // observed points must never decrease, and every snapshot must be fully readable.
        var reader = Task.Run(() =>
        {
            try
            {
                using var surface = SKSurface.Create(new SKImageInfo(64, 64));
                using var paint = new SKPaint();
                var lastCount = 0;

                while (!cts.IsCancellationRequested)
                {
                    var snapshot = stroke.GetPointsSnapshot();

                    Assert.True(
                        snapshot.Length >= lastCount,
                        $"Snapshot shrank: {snapshot.Length} < {lastCount}"
                    );
                    lastCount = snapshot.Length;

                    foreach (var p in snapshot)
                    {
                        _ = p.X + p.Y + (p.Pressure ?? 1) + (p.IsPen ? 1 : 0);
                    }

                    PaintCanvasViewModel.RenderLiveStroke(surface.Canvas, stroke, paint);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        await Task.WhenAll(writer, reader);
        Assert.Null(failure);
        Assert.Equal(100_000, stroke.GetPointsSnapshot().Length);
    }

    // ==== Test B: render thread vs UI-thread mutation + export ====
    // Enabled as of Phase 5 (lock-free ownership model). Mirrors the app's REAL threading
    // contract: one thread plays the compositor render thread (RenderToSurface in a loop), one
    // thread plays the UI thread (mutations and exports are serialized on it, as they are in the
    // app via the dispatcher). This is exactly the interleaving that used to cause the native
    // use-after-free crash class: the UI thread swapping/disposing layer bitmaps and invalidating
    // the path cache while a frame was mid-render.

    [AvaloniaFact]
    public async Task Render_WhileUiThreadMutatesAndExports_DoesNotThrow()
    {
        await RunRenderMutateStress(TimeSpan.FromSeconds(2));
    }

    private static async Task RunRenderMutateStress(TimeSpan duration)
    {
        var vm = TestHelpers.CreatePaintCanvasViewModel();
        vm.CanvasSize = new System.Drawing.Size(64, 64);
        // Signal that this canvas renders on-screen so swapped-out layer bitmaps take the
        // deferred-dispose path (drained by the render loop) instead of synchronous disposal.
        vm.RefreshCanvas = () => { };
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

        // Render thread: RenderToSurface onto a locally created CPU surface, every "frame".
        var renderThread = Task.Run(() =>
        {
            try
            {
                using var surface = SKSurface.Create(new SKImageInfo(64, 64));
                Assert.NotNull(surface);

                while (!token.IsCancellationRequested)
                {
                    vm.RenderToSurface(surface!, renderBackgroundFill: true, renderBackgroundImage: true);
                }
            }
            catch (Exception ex)
            {
                Record(ex);
            }
        });

        // Simulated UI thread: mutations AND exports, serialized with each other (as the
        // dispatcher serializes them in the app) but fully concurrent with the render thread.
        var uiThread = Task.Run(() =>
        {
            try
            {
                var i = 0;
                while (!token.IsCancellationRequested)
                {
                    switch (i++ % 6)
                    {
                        case 0:
                            vm.Paths = vm.Paths.Add(TestHelpers.BuildPenStroke(SKColors.Yellow));
                            vm.ClearRedoStack();
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
                            using (var image = vm.RenderToImage())
                            {
                                Assert.NotNull(image);
                            }
                            using (var mask = vm.RenderToWhiteChannelImage())
                            {
                                Assert.NotNull(mask);
                            }
                            break;
                        case 5:
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

        await Task.WhenAll(renderThread, uiThread);

        // Dispose while conceptually "just after" rendering stopped — exercises the quiescence gate.
        vm.Dispose();

        Assert.Empty(failures);
    }
}
