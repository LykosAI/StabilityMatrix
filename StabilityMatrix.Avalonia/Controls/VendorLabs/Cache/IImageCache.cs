using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

public interface IImageCache
{
    /// <summary>
    /// Assures that item represented by Uri is cached.
    /// </summary>
    /// <param name="uri">Uri of the item</param>
    /// <param name="cancellationToken">instance of <see cref="CancellationToken"/></param>
    /// <returns>Awaitable Task</returns>
    Task PreCacheAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves item represented by Uri locally or by downloading, does not cache the item.
    /// </summary>
    /// <param name="uri">Uri of the item.</param>
    /// <param name="decodeWidth">Width (px) to downscale the image to at decode time, or 0 for source resolution.</param>
    /// <param name="cancellationToken">instance of <see cref="CancellationToken"/></param>
    /// <returns>an instance of Generic type</returns>
    Task<IImage?> GetAsync(Uri uri, int decodeWidth = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves item represented by Uri from the cache.
    /// If the item is not found in the cache, it downloads and saves before returning it to the caller.
    /// </summary>
    /// <param name="uri">Uri of the item.</param>
    /// <param name="decodeWidth">Width (px) to downscale the image to at decode time, or 0 for source resolution.</param>
    /// <param name="cancellationToken">instance of <see cref="CancellationToken"/></param>
    /// <returns>an instance of Generic type</returns>
    Task<IImage?> GetWithCacheAsync(
        Uri uri,
        int decodeWidth = 0,
        CancellationToken cancellationToken = default
    );

    int ClearMemoryCache();

    int ClearMemoryCache(DateTime olderThan);
}
