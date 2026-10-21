using System.Formats.Tar;
using System.IO.Compression;

namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Extracts gzip-compressed tar archives containing directories, regular files, and relative file symlinks.
/// Links must target regular files inside the destination. Hard links and special files are rejected.
/// </summary>
public static class TarGZipExtractor
{
    /// <summary>
    /// Extracts files with their Unix permissions and creates links after their targets have been extracted.
    /// Existing symbolic links or junctions beneath the destination are never followed or overwritten.
    /// </summary>
    public static async Task ExtractAsync(string archivePath, string outputDirectory)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        Directory.CreateDirectory(root);
        EnsureNoLinks(root, root);

        var links = new List<(string Path, string Target, string Name)>();
        var files = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
        );
        await using var archive = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        await using var reader = new TarReader(gzip);
        while (await reader.GetNextEntryAsync().ConfigureAwait(false) is { } entry)
        {
            var path = GetContainedPath(root, root, entry.Name);
            EnsureNoLinks(root, path);
            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(path);
                    break;
                case TarEntryType.RegularFile:
                case TarEntryType.V7RegularFile:
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await entry.ExtractToFileAsync(path, overwrite: true).ConfigureAwait(false);
                    files.Add(path);
                    break;
                case TarEntryType.SymbolicLink:
                    var target = GetContainedPath(root, Path.GetDirectoryName(path)!, entry.LinkName);
                    links.Add((path, target, entry.LinkName));
                    break;
                default:
                    throw new IOException($"Unsupported tar entry type: {entry.EntryType}.");
            }
        }

        // Create links after files so no archive entry can write through an archive-created link.
        // Requiring regular targets also prevents link chains and directory-link traversal.
        foreach (var (path, target, name) in links)
        {
            EnsureNoLinks(root, path);
            EnsureNoLinks(root, target);
            if (!files.Contains(target) || !File.Exists(target))
            {
                throw new IOException($"Tar link target is not a regular file: {name}.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Use the checked path: retaining segments such as "link/../file" could traverse
            // an existing link even though the normalized target is inside the destination.
            File.CreateSymbolicLink(path, Path.GetRelativePath(Path.GetDirectoryName(path)!, target));
        }
    }

    private static string GetContainedPath(string root, string parent, string name)
    {
        // TAR uses forward slashes. Reject drive/UNC spellings as well, even on Unix.
        if (string.IsNullOrEmpty(name) || name.StartsWith('/') || name.Contains('\\') || name.Contains(':'))
        {
            throw new IOException($"Invalid tar path: {name}.");
        }

        var path = Path.GetFullPath(Path.Combine(parent, name));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (
            !path.Equals(root, comparison) && !path.StartsWith(root + Path.DirectorySeparatorChar, comparison)
        )
        {
            throw new IOException($"Tar path is outside the destination: {name}.");
        }
        return path;
    }

    private static void EnsureNoLinks(string root, string path)
    {
        var current = path;
        while (true)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Tar extraction cannot traverse a link: {current}.");
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }

            if (current == root)
            {
                return;
            }
            current = Path.GetDirectoryName(current)!;
        }
    }
}
