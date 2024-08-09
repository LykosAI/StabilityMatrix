// Parts of this file was taken from Windows Community Toolkit CacheBase implementation
// https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/Microsoft.Toolkit.Uwp.UI/Cache/ImageCache.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Extensions;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

internal class MemoryImageCache : IImageCache
{
    private class ConcurrentRequest
    {
        public Task<IImage?>? Task { get; init; }
    }

    private readonly InMemoryStorage<IImage?>? _inMemoryFileStorage = new();

    private readonly ConcurrentDictionary<string, ConcurrentRequest> _concurrentTasks = new();

    private HttpClient? _httpClient;

    /// <summary>
    /// Gets or sets the life duration of every cache entry.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the number of retries trying to ensure the file is cached.
    /// </summary>
    public uint RetryCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets max in-memory item storage count
    /// </summary>
    public int MaxMemoryCacheCount
    {
        get => _inMemoryFileStorage?.MaxItemCount ?? 0;
        set
        {
            if (_inMemoryFileStorage != null)
                _inMemoryFileStorage.MaxItemCount = value;
        }
    }

    /// <summary>
    /// Gets instance of <see cref="HttpClient"/>
    /// </summary>
    protected HttpClient HttpClient
    {
        get
        {
            if (_httpClient == null)
            {
                var messageHandler = new HttpClientHandler();

                _httpClient = new HttpClient(messageHandler);
            }

            return _httpClient;
        }
    }

    /// <summary>
    /// Initializes FileCache and provides root folder and cache folder name
    /// </summary>
    /// <param name="folder">Folder that is used as root for cache</param>
    /// <param name="folderName">Cache folder name</param>
    /// <param name="httpMessageHandler">instance of <see cref="HttpMessageHandler"/></param>
    /// <returns>awaitable task</returns>
    public virtual async Task InitializeAsync(
        string? folder = null,
        string? folderName = null,
        HttpMessageHandler? httpMessageHandler = null
    )
    {
        if (httpMessageHandler != null)
        {
            _httpClient = new HttpClient(httpMessageHandler);
        }
    }

    /// <summary>
    /// Clears all files in the cache
    /// </summary>
    public void Clear()
    {
        _inMemoryFileStorage?.Clear();
    }

    /// <summary>
    /// Removes cached images that have expired
    /// </summary>
    /// <param name="duration">Optional timespan to compute whether file has expired or not. If no value is supplied, <see cref="CacheDuration"/> is used.</param>
    public void ClearExpired(TimeSpan? duration = null)
    {
        var expiryDuration = duration ?? CacheDuration;

        _inMemoryFileStorage?.Clear(expiryDuration);
    }

    /// <summary>
    /// Removed items based on uri list passed
    /// </summary>
    /// <param name="uriForCachedItems">Enumerable uri list</param>
    /// <returns>awaitable Task</returns>
    public void Remove(IEnumerable<Uri> uriForCachedItems)
    {
        _inMemoryFileStorage?.Remove(uriForCachedItems.Select(GetCacheFileName));
    }

    /// <summary>
    /// Assures that item represented by Uri is cached.
    /// </summary>
    /// <param name="uri">Uri of the item</param>
    /// <param name="throwOnError">Indicates whether or not exception should be thrown if item cannot be cached</param>
    /// <param name="storeToMemoryCache">Indicates if item should be loaded into the in-memory storage</param>
    /// <param name="cancellationToken">instance of <see cref="CancellationToken"/></param>
    /// <returns>Awaitable Task</returns>
    public Task PreCacheAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        return GetWithCacheAsync(uri, cancellationToken);
    }

    /// <summary>
    /// Retrieves item represented by Uri from the in-memory cache if it exists and is not out of date. If item is not found or is out of date, default instance of the generic type is returned.
    /// </summary>
    /// <param name="uri">Uri of the item.</param>
    /// <returns>an instance of Generic type</returns>
    public IImage? GetFromMemoryCache(Uri uri)
    {
        var fileName = GetCacheFileName(uri);

        if (_inMemoryFileStorage?.MaxItemCount > 0)
        {
            var msi = _inMemoryFileStorage?.GetItem(fileName, CacheDuration);
            if (msi != null)
            {
                return msi.Item;
            }
        }

        return null;
    }

    private static string GetCacheFileName(Uri uri)
    {
        return CreateHash64(uri.ToString()).ToString();
    }

    private static ulong CreateHash64(string str)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(str);

        var value = (ulong)utf8.Length;
        for (var n = 0; n < utf8.Length; n++)
        {
            value += (ulong)utf8[n] << ((n * 5) % 56);
        }

        return value;
    }

    public async Task<IImage?> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        IImage? instance = null;

        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                // Local
                if (File.Exists(uri.LocalPath))
                {
                    instance = LoadLocalImageWithSkia(uri);
                }
                // Remote
                else
                {
                    instance = await DownloadImageAsync(uri, cancellationToken).ConfigureAwait(false);
                }

                if (instance != null)
                {
                    break;
                }
            }
            catch (FileNotFoundException) { }
        }

        return instance;
    }

    public async Task<IImage?> GetWithCacheAsync(Uri uri, CancellationToken cancellationToken)
    {
        IImage? instance = null;

        var fileName = GetCacheFileName(uri);
        _concurrentTasks.TryGetValue(fileName, out var request);

        if (request != null)
        {
            if (request.Task != null)
                await request.Task.ConfigureAwait(false);
            request = null;
        }

        if (request == null)
        {
            request = new ConcurrentRequest
            {
                Task = GetItemWithCacheAsync(uri, fileName, cancellationToken),
            };

            _concurrentTasks[fileName] = request;
        }

        try
        {
            if (request.Task != null)
            {
                instance = await request.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _concurrentTasks.TryRemove(fileName, out _);
        }

        return instance;
    }

    public int ClearMemoryCache()
    {
        var count = _inMemoryFileStorage?.Count ?? 0;

        if (count > 0)
        {
            _inMemoryFileStorage!.Clear();
        }

        return count;
    }

    public int ClearMemoryCache(DateTime olderThan)
    {
        var count = _inMemoryFileStorage?.Count ?? 0;

        if (count > 0)
        {
            _inMemoryFileStorage!.Clear(olderThan);
        }

        return count;
    }

    private async Task<IImage?> GetItemWithCacheAsync(
        Uri uri,
        string cacheKey,
        CancellationToken cancellationToken
    )
    {
        IImage? instance = null;

        if (_inMemoryFileStorage?.MaxItemCount > 0)
        {
            var msi = _inMemoryFileStorage?.GetItem(cacheKey, CacheDuration);
            if (msi != null)
            {
                instance = msi.Item;
            }
        }

        if (instance != null)
        {
            return instance;
        }

        var isLocal = File.Exists(uri.LocalPath);

        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                if (isLocal)
                {
                    instance = LoadLocalImageWithSkia(uri);
                }
                else
                {
                    instance = await DownloadImageAsync(uri, cancellationToken).ConfigureAwait(false);
                }

                if (instance != null)
                {
                    break;
                }
            }
            catch (FileNotFoundException) { }
        }

        // Cache the item
        if (instance != null && _inMemoryFileStorage?.MaxItemCount > 0)
        {
            var msi = new InMemoryStorageItem<IImage?>(cacheKey, DateTime.Now, instance);
            _inMemoryFileStorage?.SetItem(msi);
        }

        return instance;
    }

    private static async Task<IImage?> LoadLocalImageAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();

        await using (var stream = File.Open(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await stream.CopyToAsync(ms, cancellationToken);
            await ms.FlushAsync(cancellationToken);
        }

        ms.Position = 0;

        var image = new Bitmap(ms);
        return image;
    }

    private static IImage? LoadLocalImageWithSkia(Uri uri)
    {
        using var skFileStream = new SKFileStream(uri.LocalPath);

        return SKBitmap.Decode(skFileStream).ToAvaloniaImage();
    }

    private async Task<IImage?> DownloadImageAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();

        await using (var stream = await HttpClient.GetStreamAsync(uri, cancellationToken))
        {
            await stream.CopyToAsync(ms, cancellationToken);
            await ms.FlushAsync(cancellationToken);
        }

        ms.Position = 0;

        var image = new Bitmap(ms);
        return image;
    }
}
