using System;
using System.Threading;
using StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

namespace StabilityMatrix.Avalonia.Models;

public static class ImageCacheProviders
{
    private static readonly Lazy<IImageCache> OutputsPageImageCacheLazy =
        new(
            () =>
                new MemoryImageCache
                {
                    CacheDuration = TimeSpan.FromMinutes(10),
                    MaxMemoryCacheCount = 50,
                    RetryCount = 2
                },
            LazyThreadSafetyMode.ExecutionAndPublication
        );

    public static IImageCache OutputsPageImageCache => OutputsPageImageCacheLazy.Value;
}
