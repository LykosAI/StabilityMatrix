using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Extensions;

public static class ClipboardExtensions
{
    private static IStorageProvider StorageProvider => App.StorageProvider;

    /// <summary>
    /// Set file paths to the clipboard.
    /// </summary>
    /// <exception cref="IOException">Thrown if unable to get file from path</exception>
    public static async Task SetFileDataObjectAsync(this IClipboard clipboard, params string[] filePaths)
    {
        await clipboard.SetFileDataObjectAsync((IEnumerable<string>)filePaths);
    }

    /// <summary>
    /// Set file paths to the clipboard.
    /// </summary>
    /// <exception cref="IOException">Thrown if unable to get file from path</exception>
    public static async Task SetFileDataObjectAsync(this IClipboard clipboard, IEnumerable<string> filePaths)
    {
        var files = new List<IStorageFile>();

        foreach (var filePath in filePaths)
        {
            // Normalize path that might have come from avalonia storage provider already
            var normalizedPath = filePath.StripStart("file:///").StripStart("file://");

            var file = await StorageProvider.TryGetFileFromPathAsync(normalizedPath);
            if (file is null)
            {
                throw new IOException($"File {normalizedPath} ({filePath}) was not found");
            }

            files.Add(file);
        }

        if (files.Count == 0)
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.Set(DataFormats.Files, files);

        await clipboard.SetDataObjectAsync(dataObject);
    }
}
