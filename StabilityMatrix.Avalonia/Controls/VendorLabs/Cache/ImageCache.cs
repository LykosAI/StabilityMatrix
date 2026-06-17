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
    // CacheBase. Note the in-memory cache in CacheBase is keyed by Uri only, so a given Uri is cached at the
    // first width it was requested with; this cache is used for remote images displayed at a consistent size.
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
        return await GetFromCacheAsync(uri, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IImage?> GetWithCacheAsync(
        Uri uri,
        int decodeWidth = 0,
        CancellationToken cancellationToken = default
    )
    {
        CurrentDecodeWidth.Value = decodeWidth;
        return await GetFromCacheAsync(uri, true, cancellationToken).ConfigureAwait(false);
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
