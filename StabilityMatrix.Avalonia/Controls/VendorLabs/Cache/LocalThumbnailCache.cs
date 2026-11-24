using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Hashing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using NLog;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

/// <summary>
/// Downscaled copies of local images, kept on disk so a grid of large files decodes small images after the
/// first view. The source file is never modified. A thumbnail is keyed by the source's path, length, last
/// write time and the requested width, so an edited or replaced file gets a fresh one.
/// </summary>
[Localizable(false)]
internal sealed class LocalThumbnailCache(string folder)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Thumbnails nobody has shown for this long are deleted.</summary>
    private static readonly TimeSpan UnusedLifetime = TimeSpan.FromDays(30);

    /// <summary>A hit refreshes the thumbnail's write time at most this often, which is what pruning reads.</summary>
    private static readonly TimeSpan TouchInterval = TimeSpan.FromDays(1);

    private const int WebpQuality = 85;

    private int pruneStarted;

    public static LocalThumbnailCache Default { get; } =
        new(Path.Combine(ImageLoaders.BaseFileCachePath, "Thumbnails"));

    /// <summary>
    /// Decodes the image at <paramref name="path"/>, downscaled to <paramref name="width"/> pixels wide when
    /// the source is wider. Returns <c>null</c> when the file isn't a decodable image.
    /// </summary>
    /// <param name="path">A local image file.</param>
    /// <param name="width">Target width in pixels; 0 or less decodes at the source resolution.</param>
    /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
    public SKBitmap? Decode(string path, int width)
    {
        if (width <= 0)
        {
            return DecodeFile(path);
        }

        var source = new FileInfo(path);
        if (!source.Exists)
        {
            throw new FileNotFoundException(null, path);
        }

        StartPruneOnce();

        var thumbnailPath = GetThumbnailPath(source, width);
        if (TryDecodeThumbnail(thumbnailPath) is { } cached)
        {
            return cached;
        }

        var bitmap = DecodeFile(path);
        if (bitmap is null || bitmap.Width <= width)
        {
            return bitmap;
        }

        var height = Math.Max(1, (int)Math.Round(bitmap.Height * ((double)width / bitmap.Width)));
        var thumbnail = bitmap.Resize(
            new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
        );

        if (thumbnail is null)
        {
            return bitmap;
        }

        bitmap.Dispose();
        TryWrite(thumbnailPath, thumbnail);

        return thumbnail;
    }

    private string GetThumbnailPath(FileInfo source, int width)
    {
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{source.FullName}\n{source.Length}\n{source.LastWriteTimeUtc.Ticks}\n{width}"
        );
        var hash = XxHash128.HashToUInt128(Encoding.UTF8.GetBytes(key));

        return Path.Combine(folder, $"{hash:x32}.webp");
    }

    private static SKBitmap? DecodeFile(string path)
    {
        using var stream = new SKFileStream(path);
        return SKBitmap.Decode(stream);
    }

    private static SKBitmap? TryDecodeThumbnail(string thumbnailPath)
    {
        try
        {
            var info = new FileInfo(thumbnailPath);
            if (!info.Exists)
            {
                return null;
            }

            if (DateTime.UtcNow - info.LastWriteTimeUtc > TouchInterval)
            {
                TryTouch(info);
            }

            if (DecodeFile(thumbnailPath) is { } bitmap)
            {
                return bitmap;
            }

            // Truncated or otherwise unreadable: drop it so the next request rebuilds it.
            info.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Logger.Debug(e, "Could not read thumbnail {Path}", thumbnailPath);
        }

        return null;
    }

    private static void TryTouch(FileInfo info)
    {
        try
        {
            info.LastWriteTimeUtc = DateTime.UtcNow;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Only delays pruning; the thumbnail itself is still good.
        }
    }

    private void TryWrite(string thumbnailPath, SKBitmap thumbnail)
    {
        // Written beside the target then moved over it, so a reader never sees a partial file.
        var tempPath = $"{thumbnailPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using var data = thumbnail.Encode(SKEncodedImageFormat.Webp, WebpQuality);
            if (data is null)
            {
                return;
            }

            Directory.CreateDirectory(folder);

            using (var file = File.Create(tempPath))
            {
                data.SaveTo(file);
            }

            File.Move(tempPath, thumbnailPath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Logger.Debug(e, "Could not write thumbnail {Path}", thumbnailPath);
            TryDelete(tempPath);
        }
    }

    private void StartPruneOnce()
    {
        if (Interlocked.Exchange(ref pruneStarted, 1) == 0)
        {
            Task.Run(Prune).SafeFireAndForget(e => Logger.Warn(e, "Thumbnail cache prune failed"));
        }
    }

    private void Prune()
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var cutoff = DateTime.UtcNow - UnusedLifetime;
        var deleted = 0;

        foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
        {
            // A crash between write and move leaves a temp file behind.
            var isStaleTemp =
                file.Extension == ".tmp" && file.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1);

            if (file.LastWriteTimeUtc < cutoff || isStaleTemp)
            {
                deleted += TryDelete(file.FullName) ? 1 : 0;
            }
        }

        if (deleted > 0)
        {
            Logger.Info("Pruned {Count} unused thumbnails", deleted);
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
