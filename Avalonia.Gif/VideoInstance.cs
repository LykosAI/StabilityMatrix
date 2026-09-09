using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Animation;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Avalonia.Gif;

/// <summary>
/// An <see cref="IGifInstance"/> backed by a video file decoded by an external ffmpeg binary.
/// </summary>
/// <remarks>
/// A single long-lived ffmpeg process pipes raw frames continuously (looping the input), a background
/// thread paces them at the source frame rate and publishes the newest one, and
/// <see cref="ProcessFrameTime"/> (called on the compositor render thread) copies that frame into the one
/// <see cref="WriteableBitmap"/> this instance owns. Memory stays at roughly one frame regardless of clip
/// length; the cost is one running ffmpeg process per playing instance. Playback is silent.
/// </remarks>
public sealed class VideoInstance : IGifInstance
{
    /// <summary>Frames are downscaled so the longest side is at most this many pixels, to cap memory/CPU.</summary>
    public const int MaxPreviewDimension = 768;

    private readonly string ffmpegPath;
    private readonly string videoPath;
    private readonly string pixelFormatArg;
    private readonly int frameWidth;
    private readonly int frameHeight;
    private readonly int frameStride;
    private readonly int frameByteSize;
    private readonly TimeSpan frameDuration;

    private readonly WriteableBitmap targetBitmap;

    // Guards the published frame buffer, the target bitmap and disposal.
    private readonly object gate = new();
    private byte[]? latestFrame;
    private long latestSeq;
    private long renderedSeq = -1;
    private bool hasRenderedFrame;

    private readonly Thread readerThread;
    private Process? process;
    private bool readerFinished;

    public IterationCount IterationCount { get; set; }
    public bool AutoStart => true;
    public CancellationTokenSource CurrentCts { get; }

    /// <summary>Unknown for a streaming source; always 0.</summary>
    public int GifFrameCount => 0;
    public PixelSize GifPixelSize { get; }
    public bool IsDisposed { get; private set; }

    private VideoInstance(
        string ffmpegPath,
        string videoPath,
        string pixelFormatArg,
        PixelFormat bitmapFormat,
        int width,
        int height,
        double fps
    )
    {
        this.ffmpegPath = ffmpegPath;
        this.videoPath = videoPath;
        this.pixelFormatArg = pixelFormatArg;
        frameWidth = width;
        frameHeight = height;
        frameStride = width * 4;
        frameByteSize = frameStride * height;
        frameDuration = TimeSpan.FromSeconds(1.0 / (fps > 0 ? fps : FfmpegProbe.FallbackFps));

        GifPixelSize = new PixelSize(width, height);
        targetBitmap = new WriteableBitmap(
            GifPixelSize,
            new Vector(96, 96),
            bitmapFormat,
            AlphaFormat.Opaque
        );

        CurrentCts = new CancellationTokenSource();

        readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "VideoInstance.Reader" };
        readerThread.Start();
    }

    /// <summary>
    /// Probes <paramref name="videoPath"/> (a local file or http(s) URL) with the ffmpeg binary at
    /// <paramref name="ffmpegPath"/> and starts streaming playback. Blocks for the probe, so call it off
    /// the UI thread; frames then arrive asynchronously.
    /// </summary>
    public static VideoInstance Load(
        string ffmpegPath,
        string videoPath,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(ffmpegPath))
            throw new FileNotFoundException("ffmpeg binary not found.", ffmpegPath);

        var isRemote =
            Uri.TryCreate(videoPath, UriKind.Absolute, out var parsedUri)
            && parsedUri.Scheme is "http" or "https";
        if (!isRemote && !File.Exists(videoPath))
            throw new FileNotFoundException("Video file not found.", videoPath);

        // `ffmpeg -i <file>` with no output exits non-zero but prints the stream banner to stderr.
        var banner = RunForStdErr(ffmpegPath, ["-hide_banner", "-i", videoPath], cancellationToken);
        var info = FfmpegProbe.Parse(banner);
        var (targetWidth, targetHeight) = ComputeTargetSize(info.Width, info.Height);

        // ffmpeg emits frames in the requested byte order; match it to the platform's bitmap layout.
        var isRgba = SKImageInfo.PlatformColorType == SKColorType.Rgba8888;
        var pixelFormatArg = isRgba ? "rgba" : "bgra";
        var bitmapFormat = isRgba ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888;

        return new VideoInstance(
            ffmpegPath,
            videoPath,
            pixelFormatArg,
            bitmapFormat,
            targetWidth,
            targetHeight,
            info.Fps
        );
    }

    private static (int Width, int Height) ComputeTargetSize(int width, int height)
    {
        var longestSide = Math.Max(width, height);
        if (longestSide > MaxPreviewDimension)
        {
            var scale = MaxPreviewDimension / (double)longestSide;
            width = (int)Math.Round(width * scale);
            height = (int)Math.Round(height * scale);
        }

        // ffmpeg's scaler requires even dimensions for most pixel formats.
        width = Math.Max(2, width - (width % 2));
        height = Math.Max(2, height - (height % 2));
        return (width, height);
    }

    private void ReaderLoop()
    {
        try
        {
            var token = CurrentCts.Token;
            token.ThrowIfCancellationRequested();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in BuildStreamArgs())
                startInfo.ArgumentList.Add(arg);

            var proc = new Process { StartInfo = startInfo };
            process = proc;
            proc.Start();

            if (token.IsCancellationRequested)
                return;

            // Drain stderr so a full pipe never stalls ffmpeg.
            proc.ErrorDataReceived += (_, _) => { };
            proc.BeginErrorReadLine();

            var stdout = proc.StandardOutput.BaseStream;
            var buffer = new byte[frameByteSize];

            // Pace against a wall clock rather than sleeping a frame duration per frame, so decode time
            // doesn't accumulate into playback drift.
            var clock = Stopwatch.StartNew();
            long frameIndex = 0;

            while (!token.IsCancellationRequested)
            {
                if (!ReadExactly(stdout, buffer, token))
                    break;

                // Publish a copy so the render thread always reads a stable, complete frame.
                var frame = new byte[frameByteSize];
                Buffer.BlockCopy(buffer, 0, frame, 0, frameByteSize);
                lock (gate)
                {
                    latestFrame = frame;
                    latestSeq++;
                }

                frameIndex++;
                var due = TimeSpan.FromTicks(frameDuration.Ticks * frameIndex);
                var wait = due - clock.Elapsed;
                if (wait > TimeSpan.Zero && token.WaitHandle.WaitOne(wait))
                    break;
            }
        }
        catch (Exception)
        {
            // Reader is best-effort; on any failure playback simply stops on the last frame shown.
        }
        finally
        {
            KillProcess();
            process?.Dispose();
            lock (gate)
            {
                readerFinished = true;
                if (IsDisposed)
                    CurrentCts.Dispose();
            }
        }
    }

    private IReadOnlyList<string> BuildStreamArgs() =>
        [
            "-nostdin",
            "-hide_banner",
            "-loglevel",
            "error",
            // Loop the input forever so the pipe is continuous (cheaper than respawning ffmpeg each loop).
            "-stream_loop",
            "-1",
            "-i",
            videoPath,
            "-an",
            "-vf",
            $"scale={frameWidth}:{frameHeight}",
            "-f",
            "rawvideo",
            "-pix_fmt",
            pixelFormatArg,
            "-",
        ];

    private static bool ReadExactly(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            // A blocking Read is unblocked by the process being killed in Dispose (the pipe closes -> 0).
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read <= 0)
                return false;
            offset += read;

            if (token.IsCancellationRequested)
                return false;
        }

        return true;
    }

    private static string RunForStdErr(
        string ffmpegPath,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var killOnCancel = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already gone.
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();
        stdoutTask.GetAwaiter().GetResult();

        cancellationToken.ThrowIfCancellationRequested();

        return stderr;
    }

    public WriteableBitmap? ProcessFrameTime(TimeSpan stopwatchElapsed)
    {
        if (IsDisposed || CurrentCts.IsCancellationRequested)
            return null;

        lock (gate)
        {
            if (IsDisposed)
                return null;

            // Copy the newest frame into the target bitmap only when it has changed.
            if (latestFrame is { } frame && latestSeq != renderedSeq && frame.Length >= frameByteSize)
            {
                CopyFrameInto(frame, targetBitmap);
                renderedSeq = latestSeq;
                hasRenderedFrame = true;
            }

            return hasRenderedFrame ? targetBitmap : null;
        }
    }

    private void CopyFrameInto(byte[] frame, WriteableBitmap bitmap)
    {
        using var frameBuffer = bitmap.Lock();
        var rowBytes = frameBuffer.RowBytes;
        for (var y = 0; y < frameHeight; y++)
        {
            Marshal.Copy(frame, y * frameStride, IntPtr.Add(frameBuffer.Address, y * rowBytes), frameStride);
        }
    }

    private void KillProcess()
    {
        try
        {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Not started yet or already gone.
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            CurrentCts.Cancel();
            // Unblock Read without waiting on the UI thread. The worker owns its process
            // and keeps the cancellation wait handle alive until it has stopped using it.
            KillProcess();
            targetBitmap.Dispose();
            if (readerFinished)
                CurrentCts.Dispose();
        }
    }
}
