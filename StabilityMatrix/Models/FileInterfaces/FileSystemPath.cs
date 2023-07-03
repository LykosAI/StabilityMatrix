using System.IO;

namespace StabilityMatrix.Models.FileInterfaces;

public class FileSystemPath
{
    public string FullPath { get; }

    protected FileSystemPath(string path)
    {
        FullPath = path;
    }
    
    protected FileSystemPath(FileSystemPath path) : this(path.FullPath)
    {
    }

    protected FileSystemPath(params string[] paths) : this(Path.Combine(paths))
    {
    }
}
