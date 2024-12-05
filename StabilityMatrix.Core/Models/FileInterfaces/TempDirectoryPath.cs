namespace StabilityMatrix.Core.Models.FileInterfaces;

public class TempDirectoryPath : DirectoryPath, IDisposable
{
    public TempDirectoryPath()
        : base(Path.GetTempPath(), Path.GetRandomFileName())
    {
        Directory.CreateDirectory(FullPath);
    }

    public void Dispose()
    {
        ForceDeleteDirectory(FullPath);
        GC.SuppressFinalize(this);
    }

    private static void ForceDeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        var files = Directory.GetFiles(directoryPath);
        var directories = Directory.GetDirectories(directoryPath);

        foreach (var file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var dir in directories)
        {
            ForceDeleteDirectory(dir);
        }

        File.SetAttributes(directoryPath, FileAttributes.Normal);

        Directory.Delete(directoryPath, false);
    }
}
