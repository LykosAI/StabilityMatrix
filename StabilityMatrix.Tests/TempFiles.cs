using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.ReparsePoints;

namespace StabilityMatrix.Tests;

public static class TempFiles
{
    /// <summary>
    /// Deletes a directory tree, unlinking junctions / symlinks (without following them) before
    /// recursing so linked targets are kept and link cycles cannot loop.
    /// </summary>
    public static void DeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var item in Directory.EnumerateDirectories(directory))
        {
            var info = new DirectoryInfo(item);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                info.Attributes = FileAttributes.Normal;
                info.Delete();
            }
            else
            {
                DeleteDirectory(item);
            }
        }

        Directory.Delete(directory, true);
    }

    /// <summary>
    /// Creates a directory link the way the app does: a junction on Windows, a symlink elsewhere.
    /// </summary>
    public static void CreateDirectoryLink(string link, string target)
    {
        if (Compat.IsWindows)
        {
            Junction.Create(link, target, true);
        }
        else
        {
            Directory.CreateSymbolicLink(link, target);
        }
    }
}
