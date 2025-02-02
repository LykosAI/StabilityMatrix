using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using Blake3;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Webp;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models;

public record ImageSource : IDisposable, ITemplateKey<ImageSourceTemplateType>
{
    private Hash? contentHashBlake3;

    /// <summary>
    /// Local file path
    /// </summary>
    public FilePath? LocalFile { get; init; }

    /// <summary>
    /// Remote URL
    /// </summary>
    public Uri? RemoteUrl { get; init; }

    /// <summary>
    /// Bitmap
    /// </summary>
    [JsonIgnore]
    public Bitmap? Bitmap { get; set; }

    /// <summary>
    /// Optional label for the image
    /// </summary>
    public string? Label { get; set; }

    [JsonConstructor]
    public ImageSource() { }

    public ImageSource(FilePath localFile)
    {
        LocalFile = localFile;
    }

    public ImageSource(Uri remoteUrl)
    {
        RemoteUrl = remoteUrl;
    }

    public ImageSource(Bitmap bitmap)
    {
        Bitmap = bitmap;
    }

    [JsonIgnore]
    public Uri? Uri => LocalFile?.FullPath != null ? new Uri(LocalFile.FullPath) : RemoteUrl;

    /// <inheritdoc />
    public ImageSourceTemplateType TemplateKey { get; private set; }

    private async Task<bool> TryRefreshTemplateKeyAsync()
    {
        if ((LocalFile?.Extension ?? Path.GetExtension(RemoteUrl?.ToString())) is not { } extension)
        {
            return false;
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            if (LocalFile is not null && LocalFile.Exists)
            {
                await using var stream = LocalFile.Info.OpenRead();
                using var reader = new WebpReader(stream);

                try
                {
                    TemplateKey = reader.GetIsAnimatedFlag()
                        ? ImageSourceTemplateType.WebpAnimation
                        : ImageSourceTemplateType.Image;
                }
                catch (InvalidDataException)
                {
                    return false;
                }

                return true;
            }

            if (RemoteUrl is not null)
            {
                var httpClientFactory = App.Services.GetRequiredService<IHttpClientFactory>();
                using var client = httpClientFactory.CreateClient();

                try
                {
                    await using var stream = await client.GetStreamAsync(RemoteUrl);
                    using var reader = new WebpReader(stream);

                    TemplateKey = reader.GetIsAnimatedFlag()
                        ? ImageSourceTemplateType.WebpAnimation
                        : ImageSourceTemplateType.Image;
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            TemplateKey = ImageSourceTemplateType.WebpAnimation;
            return true;
        }

        TemplateKey = ImageSourceTemplateType.Image;

        return true;
    }

    public async Task<ImageSourceTemplateType> GetOrRefreshTemplateKeyAsync()
    {
        if (TemplateKey is ImageSourceTemplateType.Default)
        {
            await TryRefreshTemplateKeyAsync();
        }

        return TemplateKey;
    }

    [JsonIgnore]
    public Task<ImageSourceTemplateType> TemplateKeyAsync => GetOrRefreshTemplateKeyAsync();

    [JsonIgnore]
    public Task<Bitmap?> BitmapAsync => GetBitmapAsync();

    /// <summary>
    /// Get the bitmap
    /// </summary>
    public async Task<Bitmap?> GetBitmapAsync()
    {
        if (Bitmap?.Format != null)
            return Bitmap;

        var loader = ImageLoader.AsyncImageLoader;

        // Use local file path if available, otherwise remote URL
        var path = LocalFile?.FullPath ?? RemoteUrl?.ToString();

        if (path is null)
            return null;

        // Load the image
        Bitmap = await loader.ProvideImageAsync(path).ConfigureAwait(false);
        return Bitmap;
    }

    public async Task<Hash> GetBlake3HashAsync()
    {
        // Use cached value if available
        if (contentHashBlake3 is not null)
        {
            return contentHashBlake3.Value;
        }

        if (LocalFile is not null)
        {
            var data = await LocalFile.ReadAllBytesAsync();
            contentHashBlake3 = await FileHash.GetBlake3ParallelAsync(data);
        }
        else
        {
            if (await GetBitmapAsync() is not { } bitmap)
            {
                throw new InvalidOperationException("GetBitmapAsync returned null");
            }

            contentHashBlake3 = await FileHash.GetBlake3ParallelAsync(bitmap.ToByteArray());
        }

        return contentHashBlake3.Value;
    }

    /// <summary>
    /// Return a file name with Guid from Blake3 hash
    /// </summary>
    public async Task<string> GetHashGuidFileNameAsync()
    {
        var hash = await GetBlake3HashAsync();
        var guid = hash.ToGuid().ToString();

        if (LocalFile?.Extension is { } extension)
        {
            guid += extension;
        }
        else
        {
            // Default to PNG if no extension
            guid += ".png";
        }

        return guid;
    }

    /// <summary>
    /// Return a file name with Guid from Blake3 hash
    /// This will throw if the Blake3 hash has not been calculated yet
    /// </summary>
    public string GetHashGuidFileNameCached()
    {
        // Calculate hash if not available
        if (contentHashBlake3 is null)
        {
            // Local file
            if (LocalFile is not null)
            {
                // File must exist
                if (!LocalFile.Exists)
                {
                    throw new FileNotFoundException("Image file does not exist", LocalFile);
                }

                // Fail in debug since hash should have been pre-calculated
                Debug.Fail("Hash has not been calculated when GetHashGuidFileNameCached() was called");

                var data = LocalFile.ReadAllBytes();
                contentHashBlake3 = FileHash.GetBlake3Parallel(data);
            }
            // Bitmap
            else if (Bitmap is not null)
            {
                var data = Bitmap.ToByteArray();
                contentHashBlake3 = FileHash.GetBlake3Parallel(data);
            }
            else
            {
                throw new InvalidOperationException("ImageSource is not a local file or bitmap");
            }
        }

        var guid = contentHashBlake3.Value.ToGuid().ToString();

        if (LocalFile?.Extension is { } extension)
        {
            guid += extension;
        }
        else
        {
            // Default to PNG if no extension
            guid += ".png";
        }

        return guid;
    }

    public string GetHashGuidFileNameCached(string pathPrefix)
    {
        return Path.Combine(pathPrefix, GetHashGuidFileNameCached());
    }

    /// <summary>
    /// Clears the cached bitmap
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        Bitmap = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return LocalFile?.FullPath ?? RemoteUrl?.ToString() ?? "";
    }

    /// <summary>
    /// Implicit conversion to string for async image loader.
    /// Resolves with the local file path if available, otherwise the remote URL.
    /// Otherwise returns null.
    /// </summary>
    public static implicit operator string(ImageSource imageSource) => imageSource.ToString();
}
