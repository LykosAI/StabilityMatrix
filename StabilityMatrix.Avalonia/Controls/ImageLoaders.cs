using System;
using System.Threading;
using StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

namespace StabilityMatrix.Avalonia.Controls;

internal static class ImageLoaders
{
    private static readonly Lazy<MemoryImageCache> OutputsPageImageCacheLazy =
        new(
            () => new MemoryImageCache { MaxMemoryCacheCount = 64 },
            LazyThreadSafetyMode.ExecutionAndPublication
        );

    public static IImageCache OutputsPageImageCache => OutputsPageImageCacheLazy.Value;
}
