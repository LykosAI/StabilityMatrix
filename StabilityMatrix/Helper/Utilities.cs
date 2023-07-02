using System.IO;
using System.Linq;
using System.Reflection;

namespace StabilityMatrix.Helper;

public static class Utilities
{
    public static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version == null
            ? "(Unknown)"
            : $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive,
        bool includeReparsePoints = false)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        var dirs = includeReparsePoints
            ? dir.GetDirectories()
            : dir.GetDirectories().Where(d => !d.Attributes.HasFlag(FileAttributes.ReparsePoint));

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            if (file.FullName == targetFilePath) continue;
            file.CopyTo(targetFilePath, true);
        }

        if (!recursive) return;

        // If recursive and copying subdirectories, recursively call this method
        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir, true);
        }
    }
}
