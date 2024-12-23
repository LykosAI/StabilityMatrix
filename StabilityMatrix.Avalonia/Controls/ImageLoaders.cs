using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Apizr;
using Fusillade;
using StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

namespace StabilityMatrix.Avalonia.Controls;

[Localizable(false)]
internal static class ImageLoaders
{
    private static string BaseFileCachePath => Path.Combine(Path.GetTempPath(), "StabilityMatrix", "Cache");

    private static readonly Lazy<MemoryImageCache> OutputsPageImageCacheLazy =
        new(
            () => new MemoryImageCache { MaxMemoryCacheCount = 64 },
            LazyThreadSafetyMode.ExecutionAndPublication
        );

    public static IImageCache OutputsPageImageCache => OutputsPageImageCacheLazy.Value;

    private static readonly Lazy<ImageCache> OpenModelDbImageCacheLazy =
        new(
            () =>
                new ImageCache(
                    new CacheOptions
                    {
                        BaseCachePath = BaseFileCachePath,
                        CacheFolderName = "OpenModelDbImageCache",
                        CacheDuration = TimeSpan.FromDays(1),
                        HttpClient = new HttpClient(NetCache.Background)
                        {
                            DefaultRequestHeaders =
                            {
                                UserAgent = { new ProductInfoHeaderValue("StabilityMatrix", "2.0") },
                                Referrer = new Uri("https://openmodelsdb.info/"),
                            }
                        }
                    }
                ),
            LazyThreadSafetyMode.ExecutionAndPublication
        );

    public static IImageCache OpenModelDbImageCache => OpenModelDbImageCacheLazy.Value;
}
