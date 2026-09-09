using NLog;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Helper;

/// <summary>
/// File-system operations that stay correct when directories are symbolic links or junctions.
/// Links are followed, but a directory is never visited twice, so a link that loops back into
/// its own ancestry cannot recurse forever.
/// </summary>
public static class LinkSafeFileSystem
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Maximum directory nesting followed by <see cref="EnumerateFiles"/> before a subtree is skipped.
    /// </summary>
    public const int DefaultMaxDepth = 64;

    private const int MaxLinkHops = 40;

    // Windows and macOS file systems are case-insensitive by default
    private static readonly StringComparison PathComparison =
        Compat.IsWindows || Compat.IsMacOS ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly StringComparer PathComparer = StringComparer.FromComparison(PathComparison);

    /// <summary>
    /// Resolves every symbolic link or junction along <paramref name="path"/>, including in its
    /// ancestors, returning the physical directory path. Segments that cannot be resolved
    /// (missing, or a reparse point that is not a link) are kept as written.
    /// </summary>
    public static string GetRealPath(string path) => GetRealPath(path, 0);

    private static string GetRealPath(string path, int hop)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var current = root;

        foreach (
            var segment in fullPath[root.Length..]
                .Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries
                )
        )
        {
            current = Path.Join(current, segment);

            var info = new DirectoryInfo(current);
            if (!info.Exists || !info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            if (hop >= MaxLinkHops)
            {
                Logger.Warn("Gave up resolving links at {Path}: too many nested links", current);
                continue;
            }

            FileSystemInfo? target;
            try
            {
                target = info.ResolveLinkTarget(returnFinalTarget: true);
            }
            catch (IOException e)
            {
                Logger.Debug(e, "Could not resolve link target of {Path}", current);
                continue;
            }

            if (target is null)
                continue;

            // The target may itself sit below other links, so canonicalize it as a whole
            current = GetRealPath(target.FullName, hop + 1);
        }

        return current;
    }

    /// <summary>
    /// Whether creating a link at <paramref name="linkPath"/> pointing to <paramref name="sourceDir"/>
    /// would make <paramref name="sourceDir"/> reachable from inside itself, i.e. the link's parent
    /// physically is, or lies within, the source directory.
    /// </summary>
    public static bool WouldLinkCycle(DirectoryPath sourceDir, DirectoryPath linkPath)
    {
        if (linkPath.Parent is not { } linkParent)
            return false;

        var sourceReal = GetRealPath(sourceDir);
        var parentReal = GetRealPath(linkParent);

        return PathComparer.Equals(sourceReal, parentReal)
            || parentReal.StartsWith(sourceReal + Path.DirectorySeparatorChar, PathComparison);
    }

    /// <summary>
    /// Recursively enumerates files matching <paramref name="searchPattern"/> under
    /// <paramref name="rootDir"/>. Linked directories are followed once; a link back to a directory
    /// already visited is skipped, as are directories deeper than <paramref name="maxDepth"/>.
    /// Inaccessible directories are skipped rather than aborting the enumeration.
    /// Yielded paths are rooted at <paramref name="rootDir"/> as given, not at its resolved target.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(
        string rootDir,
        string searchPattern,
        int maxDepth = DefaultMaxDepth
    )
    {
        var visited = new HashSet<string>(PathComparer);
        var pending = new Stack<(string Path, string RealPath, int Depth)>();

        var rootReal = GetRealPath(rootDir);
        visited.Add(rootReal);
        pending.Push((rootDir, rootReal, 0));

        while (pending.TryPop(out var dir))
        {
            List<string> files;
            List<DirectoryInfo> subDirs;
            try
            {
                files = Directory
                    .EnumerateFiles(dir.Path, searchPattern, EnumerationOptionConstants.TopLevelOnly)
                    .ToList();
                subDirs = new DirectoryInfo(dir.Path)
                    .EnumerateDirectories("*", EnumerationOptionConstants.TopLevelOnly)
                    .ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Logger.Debug(e, "Skipping unreadable directory {Path}", dir.Path);
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (dir.Depth >= maxDepth)
            {
                Logger.Warn(
                    "Skipping directories below {Path}: nesting deeper than {MaxDepth}",
                    dir.Path,
                    maxDepth
                );
                continue;
            }

            // Pushed in reverse so the stack pops them in enumeration order
            for (var i = subDirs.Count - 1; i >= 0; i--)
            {
                var subDir = subDirs[i];
                var subReal = subDir.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    ? GetRealPath(subDir.FullName)
                    : Path.Join(dir.RealPath, subDir.Name);

                if (!visited.Add(subReal))
                {
                    Logger.Debug("Skipping {Path}: already visited as {RealPath}", subDir.FullName, subReal);
                    continue;
                }

                pending.Push((subDir.FullName, subReal, dir.Depth + 1));
            }
        }
    }
}
