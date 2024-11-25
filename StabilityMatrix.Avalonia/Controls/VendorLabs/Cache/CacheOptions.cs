using System;
using System.Net.Http;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

public class CacheOptions
{
    private static CacheOptions? _cacheOptions;

    public static CacheOptions Default => _cacheOptions ??= new CacheOptions();

    public static void SetDefault(CacheOptions defaultCacheOptions)
    {
        _cacheOptions = defaultCacheOptions;
    }

    public string? BaseCachePath { get; set; }
    public string? CacheFolderName { get; set; }
    public TimeSpan? CacheDuration { get; set; }
    public int? MaxMemoryCacheCount { get; set; }

    public HttpMessageHandler? HttpMessageHandler { get; set; }
    public HttpClient? HttpClient { get; set; }
}
