using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AsyncImageLoader.Loaders;
using Avalonia.Media.Imaging;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia;

public readonly record struct ImageLoadFailedEventArgs(string Url, Exception Exception);

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class FallbackRamCachedWebImageLoader : RamCachedWebImageLoader
{
    private readonly WeakEventManager<ImageLoadFailedEventArgs> loadFailedEventManager = new();

    public event EventHandler<ImageLoadFailedEventArgs> LoadFailed
    {
        add => loadFailedEventManager.AddEventHandler(value);
        remove => loadFailedEventManager.RemoveEventHandler(value);
    }

    protected void OnLoadFailed(string url, Exception exception) =>
        loadFailedEventManager.RaiseEvent(
            this,
            new ImageLoadFailedEventArgs(url, exception),
            nameof(LoadFailed)
        );

    /// <summary>
    /// Attempts to load bitmap
    /// </summary>
    /// <param name="url">Target url</param>
    /// <returns>Bitmap</returns>
    protected override async Task<Bitmap?> LoadAsync(string url)
    {
        // Try to load from local file first
        if (File.Exists(url))
        {
            try
            {
                if (!url.EndsWith("png", StringComparison.OrdinalIgnoreCase))
                    return new Bitmap(url);

                using var stream = ImageMetadata.BuildImageWithoutMetadata(url);
                return stream == null ? new Bitmap(url) : new Bitmap(stream);
            }
            catch (Exception e)
            {
                OnLoadFailed(url, e);
                return null;
            }
        }

        var internalOrCachedBitmap =
            await LoadFromInternalAsync(url).ConfigureAwait(false)
            ?? await LoadFromGlobalCache(url).ConfigureAwait(false);

        if (internalOrCachedBitmap != null)
            return internalOrCachedBitmap;

        try
        {
            var externalBytes = await LoadDataFromExternalAsync(url).ConfigureAwait(false);
            if (externalBytes == null)
                return null;

            using var memoryStream = new MemoryStream(externalBytes);
            var bitmap = new Bitmap(memoryStream);
            await SaveToGlobalCache(url, externalBytes).ConfigureAwait(false);
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<Bitmap?> LoadExternalNoCacheAsync(string url)
    {
        if (await LoadDataFromExternalAsync(url).ConfigureAwait(false) is not { } externalBytes)
        {
            return null;
        }

        using var memoryStream = new MemoryStream(externalBytes);
        var bitmap = new Bitmap(memoryStream);
        return bitmap;
    }

    public async Task<Bitmap?> LoadExternalAsync(string url)
    {
        var internalOrCachedBitmap =
            await LoadFromInternalAsync(url).ConfigureAwait(false)
            ?? await LoadFromGlobalCache(url).ConfigureAwait(false);

        if (internalOrCachedBitmap != null)
            return internalOrCachedBitmap;

        try
        {
            var externalBytes = await LoadDataFromExternalAsync(url).ConfigureAwait(false);
            if (externalBytes == null)
                return null;

            using var memoryStream = new MemoryStream(externalBytes);
            var bitmap = new Bitmap(memoryStream);
            await SaveToGlobalCache(url, externalBytes).ConfigureAwait(false);
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void RemovePathFromCache(string filePath)
    {
        var cache =
            this.GetPrivateField<ConcurrentDictionary<string, Task<Bitmap?>>>("_memoryCache")
            ?? throw new NullReferenceException("Memory cache not found");

        cache.TryRemove(filePath, out _);
    }

    public void RemoveAllNamesFromCache(string fileName)
    {
        var cache =
            this.GetPrivateField<ConcurrentDictionary<string, Task<Bitmap?>>>("_memoryCache")
            ?? throw new NullReferenceException("Memory cache not found");

        foreach (var (key, _) in cache)
        {
            if (Path.GetFileName(key) == fileName)
            {
                cache.TryRemove(key, out _);
            }
        }
    }

    public void ClearCache()
    {
        var cache =
            this.GetPrivateField<ConcurrentDictionary<string, Task<Bitmap?>>>("_memoryCache")
            ?? throw new NullReferenceException("Memory cache not found");

        cache.Clear();
    }
}
