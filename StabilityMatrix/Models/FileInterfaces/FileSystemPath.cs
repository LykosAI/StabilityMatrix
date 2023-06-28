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
    
    // Add operators to join other paths or strings
    public static FileSystemPath operator +(FileSystemPath path, FileSystemPath other) => new(Path.Combine(path.FullPath, other.FullPath));
    public static FileSystemPath operator +(FileSystemPath path, string other) => new(Path.Combine(path.FullPath, other));
    
    // Adding directory or file results in their types
    public static DirectoryPath operator +(FileSystemPath path, DirectoryPath other) => new(Path.Combine(path.FullPath, other.FullPath));
    public static FilePath operator +(FileSystemPath path, FilePath other) => new(Path.Combine(path.FullPath, other.FullPath));

    // Implicit conversions to and from string
    public static implicit operator string(FileSystemPath path) => path.FullPath;
    public static implicit operator FileSystemPath(string path) => new(path);
}
