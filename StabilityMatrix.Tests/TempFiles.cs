namespace StabilityMatrix.Tests;

public static class TempFiles
{
    // Deletes directory while handling junction folders
    public static void DeleteDirectory(string directory)
    {
        // Enumerate to delete any directory links
        foreach (var item in Directory.EnumerateDirectories(directory))
        {
            var info = new DirectoryInfo(item);
            if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                info.Delete();
            }
            else
            {
                DeleteDirectory(item);
            }
        }
    }
}
