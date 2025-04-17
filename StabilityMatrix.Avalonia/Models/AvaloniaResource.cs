using System.Diagnostics.CodeAnalysis;
using System.Text;
using Avalonia.Platform;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public readonly record struct AvaloniaResource(
    Uri UriPath,
    UnixFileMode WriteUnixFileMode = UnixFileMode.None
)
{
    /// <summary>
    /// File name component of the Uri path.
    /// </summary>
    public string FileName => Path.GetFileName(UriPath.ToString());

    /// <summary>
    /// File path relative to the 'Assets' folder.
    /// </summary>
    public Uri RelativeAssetPath =>
        new Uri("avares://StabilityMatrix.Avalonia/Assets/").MakeRelativeUri(UriPath);

    public AvaloniaResource(string uriPath, UnixFileMode writeUnixFileMode = UnixFileMode.None)
        : this(new Uri(uriPath), writeUnixFileMode) { }

    /// <summary>
    /// Opens a stream to this resource.
    /// </summary>
    public Stream Open() => AssetLoader.Open(UriPath);

    public async Task<string> ReadAsStringAsync()
    {
        await using var stream = AssetLoader.Open(UriPath);
        // Utf8 reader
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        // Read all text
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Extracts this resource to a target file path.
    /// </summary>
    public async Task ExtractTo(FilePath outputPath, bool overwrite = true)
    {
        if (outputPath.Exists)
        {
            // Skip if not overwriting
            if (!overwrite)
                return;
            // Otherwise delete the file
            outputPath.Delete();
        }
        var stream = AssetLoader.Open(UriPath);
        await using var fileStream = File.Create(outputPath);
        await stream.CopyToAsync(fileStream);
        // Write permissions
        if (!Compat.IsWindows && Compat.IsUnix && WriteUnixFileMode != UnixFileMode.None)
        {
            File.SetUnixFileMode(outputPath, WriteUnixFileMode);
        }
    }

    /// <summary>
    /// Extracts this resource to the output directory.
    /// </summary>
    public Task ExtractToDir(DirectoryPath outputDir, bool overwrite = true)
    {
        return ExtractTo(outputDir.JoinFile(FileName), overwrite);
    }
}
