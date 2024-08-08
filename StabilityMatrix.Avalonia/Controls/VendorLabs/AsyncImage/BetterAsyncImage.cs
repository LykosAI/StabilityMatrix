using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs;

/// <summary>
/// An image control that asynchronously retrieves an image using a <see cref="Uri"/>.
/// </summary>
[TemplatePart("PART_Image", typeof(Image))]
[TemplatePart("PART_PlaceholderImage", typeof(Image))]
public partial class BetterAsyncImage : TemplatedControl
{
    private static NLog.Logger Logger { get; } = NLog.LogManager.GetCurrentClassLogger();

    protected Image? ImagePart { get; private set; }
    protected Image? PlaceholderPart { get; private set; }

    private bool _isInitialized;
    private CancellationTokenSource? _setSourceCts;
    private CancellationTokenSource? _attachSourceAnimationCts;
    private AsyncImageState _state;

    private readonly Lazy<IImageCache?> _instanceImageCache;
    public IImageCache? InstanceImageCache => _instanceImageCache.Value;

    public BetterAsyncImage()
    {
        // Need to run on UI thread to get our attached property, so we cache the result
        _instanceImageCache = new Lazy<IImageCache?>(
            () => Dispatcher.UIThread.Invoke(() => GetImageCache(this))
        );
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        ImagePart = e.NameScope.Get<Image>("PART_Image");
        PlaceholderPart = e.NameScope.Get<Image>("PART_PlaceholderImage");

        _isInitialized = true;

        // Skip loading the image if we're disabled
        if (!IsEffectivelyEnabled)
            return;

        // In case property change didn't trigger the initial load, do it now
        if (State == AsyncImageState.Unloaded && Source is not null)
        {
            SetSource(Source);
        }
    }

    /// <summary>
    /// Cancels the current <see cref="_setSourceCts"/> and sets and returns a new <see cref="CancellationTokenSource"/>.
    /// </summary>
    private CancellationTokenSource CancelAndSetNewTokenSource(
        ref CancellationTokenSource? cancellationTokenSource
    )
    {
        var newTokenSource = new CancellationTokenSource();

        var oldTokenSource = Interlocked.Exchange(ref cancellationTokenSource, newTokenSource);

        if (oldTokenSource is not null)
        {
            try
            {
                oldTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        return newTokenSource;
    }

    private async void SetSource(object? source)
    {
        if (!_isInitialized)
        {
            return;
        }

        var newTokenSource = CancelAndSetNewTokenSource(ref _setSourceCts);

        // AttachSource(null, newTokenSource.Token);

        if (source == null)
        {
            return;
        }

        State = AsyncImageState.Loading;

        if (Source is IImage image)
        {
            AttachSource(image, newTokenSource.Token);

            return;
        }

        if (Source == null)
        {
            return;
        }

        var uri = Source;

        if (!uri.IsAbsoluteUri)
        {
            State = AsyncImageState.Failed;

            RaiseEvent(
                new AsyncImageFailedEventArgs(
                    new UriFormatException($"Relative paths aren't supported. Uri:{source}")
                )
            );

            return;
        }

        try
        {
            var bitmap = await Task.Run(
                async () =>
                {
                    // A small delay allows to cancel early if the image goes out of screen too fast (e.g. scrolling)
                    // The Bitmap constructor is expensive and cannot be cancelled
                    await Task.Delay(10, newTokenSource.Token);

                    if (uri.Scheme is "http" or "https")
                    {
                        return await LoadImageAsync(uri, newTokenSource.Token);
                    }

                    if (uri.Scheme == "file" && File.Exists(uri.LocalPath))
                    {
                        if (!IsCacheEnabled)
                        {
                            return new Bitmap(uri.LocalPath);
                        }

                        return await LoadImageAsync(uri, newTokenSource.Token);
                    }

                    if (uri.Scheme == "avares")
                    {
                        return new Bitmap(AssetLoader.Open(uri));
                    }

                    throw new UriFormatException($"Uri has unsupported scheme. Uri:{source}");
                },
                CancellationToken.None
            );

            newTokenSource.Token.ThrowIfCancellationRequested();

            AttachSource(bitmap, newTokenSource.Token);
        }
        catch (OperationCanceledException ex)
        {
            State = AsyncImageState.Unloaded;

            // Logger.Info(ex, "Canceled loading image from {Uri}", uri);
        }
        catch (Exception ex)
        {
            State = AsyncImageState.Failed;

            // Logger.Warn(ex, "Failed to load image from {Uri} ({Ex})", uri, ex.ToString());

            RaiseEvent(new AsyncImageFailedEventArgs(ex));
        }
        finally
        {
            newTokenSource.Dispose();
        }
    }

    private void AttachSource(IImage? image, CancellationToken cancellationToken)
    {
        if (ImagePart != null)
        {
            ImagePart.Source = image;
        }

        // Get new animation token source, cancel previous ones
        var newAnimationCts = CancelAndSetNewTokenSource(ref _attachSourceAnimationCts);

        if (image == null)
        {
            State = AsyncImageState.Unloaded;

            ImageTransition
                ?.Start(ImagePart, PlaceholderPart, true, newAnimationCts.Token)
                .ContinueWith(_ => newAnimationCts.Dispose(), CancellationToken.None);
        }
        else if (image.Size != default)
        {
            State = AsyncImageState.Loaded;

            ImageTransition
                ?.Start(PlaceholderPart, ImagePart, true, newAnimationCts.Token)
                .ContinueWith(_ => newAnimationCts.Dispose(), CancellationToken.None);

            RaiseEvent(new RoutedEventArgs(OpenedEvent));
        }
    }

    private async Task<IImage?> LoadImageAsync(Uri url, CancellationToken cancellationToken)
    {
        // Get specific cache for this control or use the default one
        var cache = InstanceImageCache;

        cache ??= BetterAsyncImageCacheProvider.DefaultCache;

        // Logger.Trace("Using ImageCache: <{Type}:{Id}>", cache.GetType().Name, cache.GetHashCode());

        if (IsCacheEnabled)
        {
            return await cache.GetWithCacheAsync(url, cancellationToken);
        }

        return await cache.GetAsync(url, cancellationToken);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // If we're disabled, don't load the image
        if (!IsEffectivelyEnabled)
            return;

        if (change.Property == SourceProperty)
        {
            SetSource(Source);
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            // When we become enabled, reload the image since it was skipped at apply template
            if (change.GetNewValue<bool>() && State == AsyncImageState.Unloaded)
            {
                SetSource(Source);
            }
        }
    }
}
