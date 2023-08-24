using System;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models;

public record ImageSource : IDisposable
{
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
    public Bitmap? Bitmap { get; init; }
    
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

    public Task<Bitmap?> BitmapAsync => GetBitmapAsync();
    
    /// <summary>
    /// Get the bitmap
    /// </summary>
    public async Task<Bitmap?> GetBitmapAsync()
    {
        if (Bitmap is not null) return Bitmap;

        var loader = ImageLoader.AsyncImageLoader;

        // Use local file path if available, otherwise remote URL
        var path = LocalFile?.FullPath ?? RemoteUrl?.ToString();

        if (path is null) return null;
        
        // Load the image
        return await loader.ProvideImageAsync(path).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Clears the cached bitmap
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        
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
