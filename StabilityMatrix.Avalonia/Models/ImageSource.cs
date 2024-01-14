using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using Blake3;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
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

    /// <inheritdoc />
    public ImageSourceTemplateType TemplateKey
    {
        get
        {
            var ext = LocalFile?.Extension ?? Path.GetExtension(RemoteUrl?.ToString());

            if (ext is not null && ext.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Check if webp is animated
                return ImageSourceTemplateType.WebpAnimation;
            }

            return ImageSourceTemplateType.Image;
        }
    }

    [JsonIgnore]
    public Task<Bitmap?> BitmapAsync => GetBitmapAsync();

    /// <summary>
    /// Get the bitmap
    /// </summary>
    public async Task<Bitmap?> GetBitmapAsync()
    {
        if (Bitmap is not null)
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

        // Only available for local files
        if (LocalFile is null)
        {
            throw new InvalidOperationException("ImageSource is not a local file");
        }

        var data = await LocalFile.ReadAllBytesAsync();
        contentHashBlake3 = await FileHash.GetBlake3ParallelAsync(data);

        return contentHashBlake3.Value;
    }

    /// <summary>
    /// Return a file name with Guid from Blake3 hash
    /// </summary>
    public async Task<string> GetHashGuidFileNameAsync()
    {
        if (LocalFile is null)
        {
            throw new InvalidOperationException("ImageSource is not a local file");
        }

        var extension = LocalFile.Info.Extension;

        var hash = await GetBlake3HashAsync();
        var guid = hash.ToGuid();

        return guid + extension;
    }

    /// <summary>
    /// Return a file name with Guid from Blake3 hash
    /// This will throw if the Blake3 hash has not been calculated yet
    /// </summary>
    public string GetHashGuidFileNameCached()
    {
        if (LocalFile is null)
        {
            throw new InvalidOperationException("ImageSource is not a local file");
        }

        // Calculate hash if not available
        if (contentHashBlake3 is null)
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

        var extension = LocalFile.Info.Extension;

        var guid = contentHashBlake3.Value.ToGuid();

        return guid + extension;
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

        Bitmap?.Dispose();
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
