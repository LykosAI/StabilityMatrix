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
    /// <paramref name="rootDir"/>. Linked directories are followed once; a link whose target has
    /// already been scanned is skipped and logged at Info naming the earlier path. Real directories
    /// are scanned before links so a link cannot shadow a real folder; a real directory that would
    /// be scanned twice is skipped and logged at Warn. Directories deeper than
    /// <paramref name="maxDepth"/> are skipped, as are inaccessible directories, which are skipped
    /// rather than aborting the enumeration. Yielded paths are rooted at <paramref name="rootDir"/>
    /// as given, not at its resolved target.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(
        string rootDir,
        string searchPattern,
        int maxDepth = DefaultMaxDepth
    )
    {
        // A real directory is keyed by its literal path, compared ordinally, so two folders whose
        // names differ only in case are both scanned. A link is keyed by its resolved target,
        // compared with the platform's case sensitivity (PathComparer), because a target is stored
        // however the link was created.
        var visitedRealDirsExact = new Dictionary<string, string>(StringComparer.Ordinal);
        var visitedRealDirsForLinkTargets = new Dictionary<string, string>(PathComparer);
        var visitedLinkTargets = new Dictionary<string, string>(PathComparer);

        // Real directories are drained to completion before any link is considered, so a link can
        // never take the identity of a real folder and shadow it out of the scan.
        var realDirs = new Stack<(string Path, string RealPath, int Depth, bool IsLink)>();
        var linkedDirs = new Stack<(string Path, string RealPath, int Depth, bool IsLink)>();

        var rootReal = GetRealPath(rootDir);
        realDirs.Push((rootDir, rootReal, 0, false));

        while (realDirs.Count > 0 || linkedDirs.Count > 0)
        {
            var dir = realDirs.Count > 0 ? realDirs.Pop() : linkedDirs.Pop();

            // Claimed on pop, not on push, so the walk order decides which spelling owns the
            // identity instead of the reversed push order.
            if (dir.IsLink)
            {
                if (
                    visitedLinkTargets.TryGetValue(dir.RealPath, out var linkClaimer)
                    || visitedRealDirsForLinkTargets.TryGetValue(dir.RealPath, out linkClaimer)
                )
                {
                    Logger.Info(
                        "Skipping {Path}: the same directory was already scanned as {ClaimedBy}",
                        dir.Path,
                        linkClaimer
                    );
                    continue;
                }

                visitedLinkTargets[dir.RealPath] = dir.Path;
            }
            else
            {
                if (visitedRealDirsExact.TryGetValue(dir.RealPath, out var realClaimer))
                {
                    Logger.Warn(
                        "Skipping {Path}: the same directory was already scanned as {ClaimedBy}",
                        dir.Path,
                        realClaimer
                    );
                    continue;
                }

                visitedRealDirsExact[dir.RealPath] = dir.Path;
                visitedRealDirsForLinkTargets[dir.RealPath] = dir.Path;
            }

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

            // Pushed in reverse so each stack pops its entries in enumeration order
            for (var i = subDirs.Count - 1; i >= 0; i--)
            {
                var subDir = subDirs[i];
                var isLinkDir = subDir.Attributes.HasFlag(FileAttributes.ReparsePoint);
                var subReal = isLinkDir ? GetRealPath(subDir.FullName) : Path.Join(dir.RealPath, subDir.Name);

                if (isLinkDir)
                {
                    linkedDirs.Push((subDir.FullName, subReal, dir.Depth + 1, true));
                }
                else
                {
                    realDirs.Push((subDir.FullName, subReal, dir.Depth + 1, false));
                }
            }
        }
    }
}
