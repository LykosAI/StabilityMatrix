using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Extensions;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

/// <summary>
/// Provides methods and tools to cache images in a folder
/// </summary>
internal class ImageCache(CacheOptions? options = null) : CacheBase<Bitmap>(options), IImageCache
{
    // Carries the requested decode width into ConvertFromAsync without threading it through the generic
    // CacheBase (which can't pass extra arguments to the decode step). The cache key itself is made
    // width-aware via WithDecodeWidthKey so different sizes of the same Uri never collide.
    private static readonly AsyncLocal<int> CurrentDecodeWidth = new();

    /// <summary>
    /// Creates a bitmap from a stream
    /// </summary>
    /// <param name="stream">input stream</param>
    /// <returns>awaitable task</returns>
    protected override async Task<Bitmap> ConvertFromAsync(Stream stream)
    {
        if (stream.Length == 0)
        {
            throw new FileNotFoundException();
        }

        return DecodeBitmap(stream, CurrentDecodeWidth.Value);
    }

    /// <summary>
    /// Decodes a stream into a bitmap, downscaling to <paramref name="decodeWidth"/> (px) if it is wider.
    /// </summary>
    private static Bitmap DecodeBitmap(Stream stream, int decodeWidth)
    {
        if (decodeWidth <= 0)
        {
            return new Bitmap(stream);
        }

        var original = stream.ToSKBitmap();
        if (original is null)
        {
            stream.Position = 0;
            return new Bitmap(stream);
        }

        using (original)
        {
            if (original.Width <= decodeWidth)
            {
                return original.ToAvaloniaBitmap();
            }

            var targetHeight = Math.Max(
                1,
                (int)Math.Round(original.Height * ((double)decodeWidth / original.Width))
            );

            using var resized = original.Resize(
                new SKImageInfo(decodeWidth, targetHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            );

            return (resized ?? original).ToAvaloniaBitmap();
        }
    }

    /// <summary>
    /// Creates a bitmap from a cached file
    /// </summary>
    /// <param name="baseFile">file</param>
    /// <returns>awaitable task</returns>
    protected override async Task<Bitmap> ConvertFromAsync(string baseFile)
    {
        await using var stream = File.OpenRead(baseFile);
        return await ConvertFromAsync(stream).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks whether file is valid or not.
    /// </summary>
    /// <param name="file">file</param>
    /// <param name="duration">cache duration</param>
    /// <param name="treatNullFileAsOutOfDate">option to mark uninitialized file as expired</param>
    /// <returns>bool indicate whether file has expired or not</returns>
    protected override async Task<bool> IsFileOutOfDateAsync(
        string? file,
        TimeSpan duration,
        bool treatNullFileAsOutOfDate = true
    )
    {
        if (file == null)
        {
            return treatNullFileAsOutOfDate;
        }

        var fileInfo = new FileInfo(file);

        return fileInfo.Length == 0
            || DateTime.Now.Subtract(File.GetLastAccessTime(file)) > duration
            || DateTime.Now.Subtract(File.GetLastWriteTime(file)) > duration;
    }

    public Task PreCacheAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        return PreCacheAsync(uri, true, true, cancellationToken);
    }

    public async Task<IImage?> GetAsync(
        Uri uri,
        int decodeWidth = 0,
        CancellationToken cancellationToken = default
    )
    {
        CurrentDecodeWidth.Value = decodeWidth;
        return await GetFromCacheAsync(WithDecodeWidthKey(uri, decodeWidth), false, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IImage?> GetWithCacheAsync(
        Uri uri,
        int decodeWidth = 0,
        CancellationToken cancellationToken = default
    )
    {
        CurrentDecodeWidth.Value = decodeWidth;
        return await GetFromCacheAsync(WithDecodeWidthKey(uri, decodeWidth), true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a Uri that includes the decode width as a fragment so the underlying cache (keyed by Uri)
    /// keeps a separate entry per size — e.g. a 450px thumbnail decode can't be served for a later
    /// full-resolution request of the same image. Uri fragments are not sent in HTTP requests, so the
    /// actual download is unaffected.
    /// </summary>
    private static Uri WithDecodeWidthKey(Uri uri, int decodeWidth)
    {
        return decodeWidth <= 0 ? uri : new UriBuilder(uri) { Fragment = $"sm-decode={decodeWidth}" }.Uri;
    }

    public int ClearMemoryCache()
    {
        var count = InMemoryFileStorage?.Count ?? 0;

        if (count > 0)
        {
            InMemoryFileStorage!.Clear();
        }

        return count;
    }

    public int ClearMemoryCache(DateTime olderThan)
    {
        var count = InMemoryFileStorage?.Count ?? 0;

        if (count > 0)
        {
            InMemoryFileStorage!.Clear(olderThan);
        }

        return count;
    }
}
