using System;

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
    public TimeSpan? CacheDuration { get; set; }
    public int? MaxMemoryCacheCount { get; set; }
}
