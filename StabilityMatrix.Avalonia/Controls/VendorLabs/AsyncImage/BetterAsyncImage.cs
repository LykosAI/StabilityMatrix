using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AsyncImageLoader;
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
    protected Image? ImagePart { get; private set; }
    protected Image? PlaceholderPart { get; private set; }

    private bool _isInitialized;
    private CancellationTokenSource? _setSourceCts;
    private CancellationTokenSource? _attachSourceAnimationCts;
    private AsyncImageState _state;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        ImagePart = e.NameScope.Get<Image>("PART_Image");
        PlaceholderPart = e.NameScope.Get<Image>("PART_PlaceholderImage");

        // _setSourceCts = new CancellationTokenSource();

        _isInitialized = true;

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

                    if (uri.Scheme == "avares")
                    {
                        return new Bitmap(AssetLoader.Open(uri));
                    }

                    if (uri.Scheme == "file" && File.Exists(uri.LocalPath))
                    {
                        return new Bitmap(uri.LocalPath);
                    }

                    throw new UriFormatException($"Uri has unsupported scheme. Uri:{source}");
                },
                CancellationToken.None
            );

            if (newTokenSource.IsCancellationRequested)
            {
                return;
            }

            AttachSource(bitmap, newTokenSource.Token);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            State = AsyncImageState.Failed;

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

    private async Task<Bitmap?> LoadImageAsync(Uri url, CancellationToken token)
    {
        /*if (await ProvideCachedResourceAsync(url, token) is { } bitmap)
        {
            return bitmap;
        }*/

        if (ImageLoader.AsyncImageLoader is not FallbackRamCachedWebImageLoader loader)
        {
            throw new InvalidOperationException(
                "ImageLoader must be an instance of FallbackRamCachedWebImageLoader"
            );
        }

        if (IsCacheEnabled)
        {
            return await loader.LoadExternalAsync(url.ToString());
        }

        return await loader.LoadExternalNoCacheAsync(url.ToString());

        /*using var client = new HttpClient();
        var stream = await client.GetStreamAsync(url, token).ConfigureAwait(false);

        await using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, token).ConfigureAwait(false);

        memoryStream.Position = 0;
        return new Bitmap(memoryStream);*/
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            SetSource(Source);
        }
    }

    protected virtual async Task<Bitmap?> ProvideCachedResourceAsync(Uri? imageUri, CancellationToken token)
    {
        if (IsCacheEnabled && imageUri != null)
        {
            return await ImageCache
                .Instance.GetFromCacheAsync(imageUri, cancellationToken: token)
                .ConfigureAwait(false);
        }
        return null;
    }
}
