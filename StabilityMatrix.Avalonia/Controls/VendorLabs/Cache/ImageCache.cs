using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

/// <summary>
/// Provides methods and tools to cache images in a folder
/// </summary>
internal class ImageCache(CacheOptions? options = null) : CacheBase<Bitmap>(options), IImageCache
{
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

        return new Bitmap(stream);
    }

    /// <summary>
    /// Creates a bitmap from a cached file
    /// </summary>
    /// <param name="baseFile">file</param>
    /// <returns>awaitable task</returns>
    protected override async Task<Bitmap> ConvertFromAsync(string baseFile)
    {
        using (var stream = File.OpenRead(baseFile))
        {
            return await ConvertFromAsync(stream).ConfigureAwait(false);
        }
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

    public async Task<IImage?> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        return await GetFromCacheAsync(uri, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IImage?> GetWithCacheAsync(Uri uri, CancellationToken cancellationToken = default)
    {
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
