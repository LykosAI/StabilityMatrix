using ImageMagick;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Animation;

public class GifConverter
{
    public static async Task ConvertWebpToGif(FilePath filePath)
    {
        using var webp = new MagickImageCollection(filePath, MagickFormat.WebP);
        var path = filePath.ToString().Replace(".webp", ".gif");
        await webp.WriteAsync(path, MagickFormat.Gif).ConfigureAwait(false);
    }
}
