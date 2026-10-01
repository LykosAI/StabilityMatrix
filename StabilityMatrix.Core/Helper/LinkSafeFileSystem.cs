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
    public static string GetRealPath(string path) => GetRealPath(path, out _);

    /// <summary>
    /// <inheritdoc cref="GetRealPath(string)"/> <paramref name="hops"/> receives how many links
    /// the path goes through.
    /// </summary>
    private static string GetRealPath(string path, out int hops)
    {
        hops = 0;
        return Resolve(path, ref hops);
    }

    private static string Resolve(string path, ref int hops)
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

            if (hops >= MaxLinkHops)
            {
                Logger.Warn("Gave up resolving links at {Path}: too many nested links", current);
                continue;
            }

            FileSystemInfo? target;
            try
            {
                // The immediate target, as stored, so it is spelled the way the link's own
                // neighbourhood is (subst and mapped drives would otherwise resolve to the volume)
                target = info.ResolveLinkTarget(returnFinalTarget: false);
            }
            catch (IOException e)
            {
                Logger.Debug(e, "Could not resolve link target of {Path}", current);
                continue;
            }

            if (target is null)
                continue;

            hops++;

            // The target may itself be a link, or sit below other links, so canonicalize it as a whole
            current = Resolve(target.FullName, ref hops);
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
    /// <paramref name="rootDir"/>. Symbolic links are followed, but every physical directory is
    /// visited at most once, so no file is yielded twice; a directory reachable along several
    /// paths is yielded under the one that goes through the fewest links. Directories nested
    /// deeper than <paramref name="maxDepth"/> and directories that cannot be read are skipped
    /// without aborting the enumeration. Yielded paths are rooted at <paramref name="rootDir"/>
    /// as given, not at its resolved target.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(
        string rootDir,
        string searchPattern,
        int maxDepth = DefaultMaxDepth
    )
    {
        // Physical directories already scanned. Links are what turn the tree into a graph.
        var seen = new HashSet<string>(PathComparer);
        // Links wait until the real tree is done, then go in order of how many links they pass
        // through, so a folder is listed under its own name rather than through an alias of it,
        // and a linked folder under its link rather than through an alias of the link
        var links = new PriorityQueue<(string Path, string RealPath, int Depth), int>();

        foreach (var file in Walk(rootDir, GetRealPath(rootDir), 0))
            yield return file;

        while (links.TryDequeue(out var link, out _))
        {
            foreach (var file in Walk(link.Path, link.RealPath, link.Depth))
                yield return file;
        }

        IEnumerable<string> Walk(string dir, string realDir, int depth)
        {
            if (!seen.Add(realDir))
            {
                Logger.Debug("Skipping {Path}: already scanned", dir);
                yield break;
            }

            List<string> files;
            List<DirectoryInfo> subDirs;
            try
            {
                files = Directory
                    .EnumerateFiles(dir, searchPattern, EnumerationOptionConstants.TopLevelOnly)
                    .ToList();
                subDirs = new DirectoryInfo(dir)
                    .EnumerateDirectories("*", EnumerationOptionConstants.TopLevelOnly)
                    .ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Logger.Debug(e, "Skipping missing or unreadable directory {Path}", dir);
                yield break;
            }

            foreach (var file in files)
                yield return file;

            if (depth >= maxDepth)
            {
                Logger.Warn(
                    "Skipping directories below {Path}: nesting deeper than {MaxDepth}",
                    dir,
                    maxDepth
                );
                yield break;
            }

            foreach (var subDir in subDirs)
            {
                if (subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var realPath = GetRealPath(subDir.FullName, out var hops);
                    links.Enqueue((subDir.FullName, realPath, depth + 1), hops);
                    continue;
                }

                foreach (var file in Walk(subDir.FullName, Path.Join(realDir, subDir.Name), depth + 1))
                    yield return file;
            }
        }
    }
}
