using Avalonia.Animation;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Avalonia.Gif;

public class WebpInstance : IGifInstance
{
    public IterationCount IterationCount { get; set; }
    public bool AutoStart { get; private set; } = true;

    private readonly WriteableBitmap? _targetBitmap;
    private TimeSpan _totalTime;
    private readonly List<TimeSpan> _frameTimes;
    private uint _iterationCount;
    private int _currentFrameIndex;

    private SKCodec? _codec;

    public CancellationTokenSource CurrentCts { get; }

    internal WebpInstance(object newValue)
        : this(
            newValue switch
            {
                Stream s => s,
                Uri u => GetStreamFromUri(u),
                string str => GetStreamFromString(str),
                _ => throw new InvalidDataException("Unsupported source object")
            }
        ) { }

    public WebpInstance(string uri)
        : this(GetStreamFromString(uri)) { }

    public WebpInstance(Uri uri)
        : this(GetStreamFromUri(uri)) { }

    public WebpInstance(Stream currentStream)
    {
        if (!currentStream.CanSeek)
            throw new InvalidDataException("The provided stream is not seekable.");

        if (!currentStream.CanRead)
            throw new InvalidOperationException("Can't read the stream provided.");

        currentStream.Seek(0, SeekOrigin.Begin);

        CurrentCts = new CancellationTokenSource();

        var managedStream = new SKManagedStream(currentStream);
        _codec = SKCodec.Create(managedStream);

        var pixSize = new PixelSize(_codec.Info.Width, _codec.Info.Height);

        _targetBitmap = new WriteableBitmap(
            pixSize,
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        GifPixelSize = pixSize;

        _totalTime = TimeSpan.Zero;

        _frameTimes = _codec
            .FrameInfo.Select(frame =>
            {
                _totalTime = _totalTime.Add(TimeSpan.FromMilliseconds(frame.Duration));
                return _totalTime;
            })
            .ToList();

        RenderFrame(_codec, _targetBitmap, 0);
    }

    private static void RenderFrame(SKCodec codec, WriteableBitmap targetBitmap, int index)
    {
        codec.GetFrameInfo(index, out var frameInfo);

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);
        var decodeInfo = info.WithAlphaType(frameInfo.AlphaType);

        using var frameBuffer = targetBitmap.Lock();

        var result = codec.GetPixels(decodeInfo, frameBuffer.Address, new SKCodecOptions(index));

        if (result != SKCodecResult.Success)
            throw new InvalidDataException($"Could not decode frame {index} of {codec.FrameCount}.");
    }

    private static void RenderFrame(SKCodec codec, WriteableBitmap targetBitmap, int index, int priorIndex)
    {
        codec.GetFrameInfo(index, out var frameInfo);

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);
        var decodeInfo = info.WithAlphaType(frameInfo.AlphaType);

        using var frameBuffer = targetBitmap.Lock();

        var result = codec.GetPixels(decodeInfo, frameBuffer.Address, new SKCodecOptions(index, priorIndex));

        if (result != SKCodecResult.Success)
            throw new InvalidDataException($"Could not decode frame {index} of {codec.FrameCount}.");
    }

    private static Stream GetStreamFromString(string str)
    {
        if (!Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out var res))
        {
            throw new InvalidCastException("The string provided can't be converted to URI.");
        }

        return GetStreamFromUri(res);
    }

    private static Stream GetStreamFromUri(Uri uri)
    {
        var uriString = uri.OriginalString.Trim();

        if (!uriString.StartsWith("resm") && !uriString.StartsWith("avares"))
        {
            return new FileStream(uriString, FileMode.Open, FileAccess.Read);
        }

        return AssetLoader.Open(uri);
    }

    public int GifFrameCount => _frameTimes.Count;

    public PixelSize GifPixelSize { get; }

    public void Dispose()
    {
        IsDisposed = true;
        CurrentCts.Cancel();
        _targetBitmap?.Dispose();
        _codec?.Dispose();
    }

    public bool IsDisposed { get; private set; }

    public WriteableBitmap? ProcessFrameTime(TimeSpan stopwatchElapsed)
    {
        if (!IterationCount.IsInfinite && _iterationCount > IterationCount.Value)
        {
            return null;
        }

        if (CurrentCts.IsCancellationRequested || _targetBitmap is null)
        {
            return null;
        }

        var elapsedTicks = stopwatchElapsed.Ticks;
        var timeModulus = TimeSpan.FromTicks(elapsedTicks % _totalTime.Ticks);
        var targetFrame = _frameTimes.FirstOrDefault(x => timeModulus < x);
        var currentFrame = _frameTimes.IndexOf(targetFrame);
        if (currentFrame == -1)
            currentFrame = 0;

        if (_currentFrameIndex == currentFrame)
            return _targetBitmap;

        _iterationCount = (uint)(elapsedTicks / _totalTime.Ticks);

        return ProcessFrameIndex(currentFrame);
    }

    internal WriteableBitmap ProcessFrameIndex(int frameIndex)
    {
        if (_codec is null)
            throw new InvalidOperationException("The codec is null.");

        if (_targetBitmap is null)
            throw new InvalidOperationException("The target bitmap is null.");

        RenderFrame(_codec, _targetBitmap, frameIndex, _currentFrameIndex);
        _currentFrameIndex = frameIndex;

        return _targetBitmap;
    }
}
