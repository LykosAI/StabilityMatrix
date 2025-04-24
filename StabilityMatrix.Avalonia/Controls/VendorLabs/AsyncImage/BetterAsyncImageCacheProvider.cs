using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Fusillade;
using StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs;

public static class BetterAsyncImageCacheProvider
{
    private static readonly Lazy<ImageCache> DefaultCacheLazy =
        new(
            () =>
                new ImageCache(
                    new CacheOptions
                    {
                        // ReSharper disable twice LocalizableElement
                        BaseCachePath =
                            Assembly.GetExecutingAssembly().FullName is { } assemblyName
                            && !string.IsNullOrEmpty(assemblyName)
                                ? Path.Combine(Path.GetTempPath(), assemblyName, "Cache")
                                : Path.Combine(Path.GetTempPath(), "Cache"),
                        CacheDuration = TimeSpan.FromDays(1),
                        HttpMessageHandler = NetCache.UserInitiated
                    }
                ),
            LazyThreadSafetyMode.ExecutionAndPublication
        );

    private static IImageCache? _defaultCache;

    public static IImageCache DefaultCache
    {
        get => _defaultCache ?? DefaultCacheLazy.Value;
        set => _defaultCache = value;
    }
}
