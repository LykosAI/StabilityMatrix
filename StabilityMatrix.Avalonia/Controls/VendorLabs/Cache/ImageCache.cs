using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

/// <summary>
/// Provides methods and tools to cache images in a folder
/// </summary>
internal class ImageCache : CacheBase<Bitmap>
{
    /// <summary>
    /// Private singleton field.
    /// </summary>
    [ThreadStatic]
    private static ImageCache? _instance;

    /// <summary>
    /// Gets public singleton property.
    /// </summary>
    public static ImageCache Instance => _instance ?? (_instance = new ImageCache());

    /// <summary>
    /// Creates a bitmap from a stream
    /// </summary>
    /// <param name="stream">input stream</param>
    /// <returns>awaitable task</returns>
    protected override async Task<Bitmap> ConvertFromAsync(Stream stream)
    {
        if (stream.Length == 0)
        {
            throw new FileNotFoundException();
        }

        return new Bitmap(stream);
    }

    /// <summary>
    /// Creates a bitmap from a cached file
    /// </summary>
    /// <param name="baseFile">file</param>
    /// <returns>awaitable task</returns>
    protected override async Task<Bitmap> ConvertFromAsync(string baseFile)
    {
        using (var stream = File.OpenRead(baseFile))
        {
            return await ConvertFromAsync(stream).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Checks whether file is valid or not.
    /// </summary>
    /// <param name="file">file</param>
    /// <param name="duration">cache duration</param>
    /// <param name="treatNullFileAsOutOfDate">option to mark uninitialized file as expired</param>
    /// <returns>bool indicate whether file has expired or not</returns>
    protected override async Task<bool> IsFileOutOfDateAsync(
        string file,
        TimeSpan duration,
        bool treatNullFileAsOutOfDate = true
    )
    {
        if (file == null)
        {
            return treatNullFileAsOutOfDate;
        }

        var fileInfo = new FileInfo(file);

        return fileInfo.Length == 0
            || DateTime.Now.Subtract(File.GetLastAccessTime(file)) > duration
            || DateTime.Now.Subtract(File.GetLastWriteTime(file)) > duration;
    }
}
