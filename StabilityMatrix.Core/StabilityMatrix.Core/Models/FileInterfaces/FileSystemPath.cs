namespace StabilityMatrix.Core.Models.FileInterfaces;

public class FileSystemPath : IEquatable<FileSystemPath>, IEquatable<string>
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
    
    public override string ToString()
    {
        return FullPath;
    }

    public bool Equals(FileSystemPath? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return FullPath == other.FullPath;
    }
    
    public bool Equals(string? other)
    {
        return string.Equals(FullPath, other);
    }

    public override bool Equals(object? obj)
    {
        return obj switch
        {
            FileSystemPath path => Equals(path),
            string path => Equals(path),
            _ => false
        };
    }

    public override int GetHashCode()
    {
        return FullPath.GetHashCode();
    }
}
