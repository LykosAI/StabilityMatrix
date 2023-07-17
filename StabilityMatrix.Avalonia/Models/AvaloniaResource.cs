using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Models;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public readonly record struct AvaloniaResource(
    Uri UriPath, 
    UnixFileMode WriteUnixFileMode = UnixFileMode.None)
{
    public string FileName => UriPath.Segments[^1];

    public AvaloniaResource(string uriPath, UnixFileMode writeUnixFileMode = UnixFileMode.None) 
        : this(new Uri(uriPath), writeUnixFileMode)
    {
    }
    
    /// <summary>
    /// Extracts this resource to the output directory.
    /// </summary>
    public async Task ExtractTo(string outputDir, bool overwrite = true)
    {
        var targetPath = Path.Combine(outputDir, FileName);
        if (File.Exists(targetPath))
        {
            // Skip if not overwriting
            if (!overwrite) return;
            // Otherwise delete the file
            File.Delete(targetPath);
        }
        var stream = AssetLoader.Open(UriPath);
        await using var fileStream = File.Create(targetPath);
        await stream.CopyToAsync(fileStream);
        // Write permissions
        if (!Compat.IsWindows && Compat.IsUnix && WriteUnixFileMode != UnixFileMode.None)
        {
            File.SetUnixFileMode(targetPath, WriteUnixFileMode);
        }
    }
}
