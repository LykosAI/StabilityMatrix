using System.Numerics;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Gif;

/// <summary>
/// Plays a video file (e.g. mp4) by decoding its frames with an external ffmpeg binary and rendering them
/// through the same compositor custom-visual path as <see cref="GifImage"/>. Set <see cref="Source"/> to
/// a local video path or http(s) URL; the ffmpeg binary comes from <see cref="FfmpegPath"/> or, when that
/// is unset, <see cref="FfmpegPathResolver"/>. Playback loops and is silent.
/// </summary>
public class VideoImage : Control
{
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<
        VideoImage,
        string?
    >(nameof(Source));

    public static readonly StyledProperty<string?> FfmpegPathProperty = AvaloniaProperty.Register<
        VideoImage,
        string?
    >(nameof(FfmpegPath));

    public static readonly StyledProperty<IterationCount> IterationCountProperty = AvaloniaProperty.Register<
        VideoImage,
        IterationCount
    >(nameof(IterationCount), IterationCount.Infinite);

    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<VideoImage, StretchDirection>(nameof(StretchDirection));

    public static readonly StyledProperty<Stretch> StretchProperty = AvaloniaProperty.Register<
        VideoImage,
        Stretch
    >(nameof(Stretch), Stretch.Uniform);

    public static readonly DirectProperty<VideoImage, bool> IsPlayingProperty =
        AvaloniaProperty.RegisterDirect<VideoImage, bool>(nameof(IsPlaying), o => o.IsPlaying);

    public static readonly DirectProperty<VideoImage, bool> HasLoadFailedProperty =
        AvaloniaProperty.RegisterDirect<VideoImage, bool>(nameof(HasLoadFailed), o => o.HasLoadFailed);

    /// <summary>
    /// App-supplied fallback for <see cref="FfmpegPath"/>, so templated usage (where binding an ffmpeg path
    /// per item is awkward) can resolve the binary globally. Set once at startup.
    /// </summary>
    public static Func<string?>? FfmpegPathResolver { get; set; }

    private IGifInstance? _videoInstance;
    private CompositionCustomVisual? _customVisual;
    private CancellationTokenSource? _loadCts;
    private bool _isPlaying;
    private bool _hasLoadFailed;

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? FfmpegPath
    {
        get => GetValue(FfmpegPathProperty);
        set => SetValue(FfmpegPathProperty, value);
    }

    public IterationCount IterationCount
    {
        get => GetValue(IterationCountProperty);
        set => SetValue(IterationCountProperty, value);
    }

    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>True once the clip has been opened and frames are being rendered.</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetAndRaise(IsPlayingProperty, ref _isPlaying, value);
    }

    /// <summary>True when the current <see cref="Source"/> could not be opened (no ffmpeg, unreadable file).</summary>
    public bool HasLoadFailed
    {
        get => _hasLoadFailed;
        private set => SetAndRaise(HasLoadFailedProperty, ref _hasLoadFailed, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        switch (change.Property.Name)
        {
            case nameof(Source):
            case nameof(FfmpegPath):
                ReloadSource();
                break;
            case nameof(Stretch):
            case nameof(StretchDirection):
                InvalidateArrange();
                InvalidateMeasure();
                Update();
                break;
            case nameof(Bounds):
                Update();
                break;
        }

        base.OnPropertyChanged(change);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor != null && _customVisual?.Compositor != compositor)
        {
            _customVisual = compositor.CreateCustomVisual(new CustomVisualHandler());
            ElementComposition.SetElementChildVisual(this, _customVisual);
            _customVisual.SendHandlerMessage(CustomVisualHandler.StartMessage);

            ReloadSource();
            Update();
        }

        base.OnAttachedToVisualTree(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopPlayback();

        if (_customVisual != null)
        {
            _customVisual.SendHandlerMessage(CustomVisualHandler.StopMessage);
            ElementComposition.SetElementChildVisual(this, null);
            _customVisual = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Cancels any in-flight decode and tears down the current clip (and its ffmpeg process).</summary>
    private void StopPlayback()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        _videoInstance?.Dispose();
        _videoInstance = null;
        IsPlaying = false;
    }

    private void ReloadSource()
    {
        // Until attached there is no compositor visual to render into; OnAttachedToVisualTree calls back in.
        if (_customVisual is null)
            return;

        StopPlayback();
        HasLoadFailed = false;

        var source = Source;
        var ffmpegPath = FfmpegPath ?? FfmpegPathResolver?.Invoke();
        if (string.IsNullOrWhiteSpace(source))
            return;

        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            HasLoadFailed = true;
            return;
        }

        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var iterationCount = IterationCount;

        // The probe is a blocking ffmpeg run; never do it on the UI thread.
        Task.Run(
            () =>
            {
                try
                {
                    var instance = VideoInstance.Load(ffmpegPath, source, cts.Token);
                    instance.IterationCount = iterationCount;

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (cts.IsCancellationRequested || !ReferenceEquals(_loadCts, cts))
                        {
                            instance.Dispose();
                            return;
                        }

                        _videoInstance = instance;
                        _customVisual?.SendHandlerMessage(instance);
                        IsPlaying = true;

                        InvalidateArrange();
                        InvalidateMeasure();
                        Update();
                    });
                }
                catch (OperationCanceledException)
                {
                    // Source changed or control detached mid-decode; nothing to do.
                }
                catch (Exception ex)
                {
                    Logger.Sink?.Log(
                        LogEventLevel.Warning,
                        "VideoImage",
                        this,
                        $"Failed to load video '{source}': {ex}"
                    );
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (ReferenceEquals(_loadCts, cts))
                            HasLoadFailed = true;
                    });
                }
            },
            cts.Token
        );
    }

    private void Update()
    {
        if (_customVisual is null || _videoInstance is null)
            return;

        var dpi = this.GetVisualRoot()?.RenderScaling ?? 1.0;
        var sourceSize = _videoInstance.GifPixelSize.ToSize(dpi);
        var viewPort = new Rect(Bounds.Size);

        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;

        // Size the visual to the aspect-correct scaled size and centre it. For UniformToFill the content is
        // larger than the viewport and the overflow is cropped by the control's ClipToBounds; intersecting
        // with the viewport here would squash the frame, since OnRender draws the full bitmap into these
        // bounds.
        var destRect = viewPort.CenterRect(new Rect(scaledSize));

        if (Stretch == Stretch.None)
        {
            _customVisual.Size = new Vector2((float)sourceSize.Width, (float)sourceSize.Height);
        }
        else
        {
            _customVisual.Size = new Vector2((float)scaledSize.Width, (float)scaledSize.Height);
        }

        _customVisual.Offset = new Vector3((float)destRect.Position.X, (float)destRect.Position.Y, 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var result = new Size();
        var scaling = this.GetVisualRoot()?.RenderScaling ?? 1.0;
        if (_videoInstance != null)
        {
            result = Stretch.CalculateSize(
                availableSize,
                _videoInstance.GifPixelSize.ToSize(scaling),
                StretchDirection
            );
        }

        return result;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_videoInstance is null)
            return new Size();

        var scaling = this.GetVisualRoot()?.RenderScaling ?? 1.0;
        var sourceSize = _videoInstance.GifPixelSize.ToSize(scaling);
        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    private class CustomVisualHandler : CompositionCustomVisualHandler
    {
        private TimeSpan _animationElapsed;
        private TimeSpan? _lastServerTime;
        private IGifInstance? _currentInstance;
        private bool _running;

        public static readonly object StopMessage = new(),
            StartMessage = new();

        public override void OnMessage(object message)
        {
            if (message == StartMessage)
            {
                _running = true;
                _lastServerTime = null;
                RegisterForNextAnimationFrameUpdate();
            }
            else if (message == StopMessage)
            {
                _running = false;
            }
            else if (message is IGifInstance instance)
            {
                _currentInstance?.Dispose();
                _currentInstance = instance;
            }
        }

        public override void OnAnimationFrameUpdate()
        {
            if (!_running)
                return;
            Invalidate();
            RegisterForNextAnimationFrameUpdate();
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            if (_running)
            {
                if (_lastServerTime.HasValue)
                    _animationElapsed += (CompositionNow - _lastServerTime.Value);
                _lastServerTime = CompositionNow;
            }

            try
            {
                if (_currentInstance is null || _currentInstance.IsDisposed)
                    return;

                var bitmap = _currentInstance.ProcessFrameTime(_animationElapsed);
                if (bitmap is not null)
                {
                    drawingContext.DrawBitmap(
                        bitmap,
                        new Rect(_currentInstance.GifPixelSize.ToSize(1)),
                        GetRenderBounds()
                    );
                }
            }
            catch (Exception e)
            {
                Logger.Sink?.Log(LogEventLevel.Error, "VideoImage Renderer", this, e.ToString());
            }
        }
    }
}
