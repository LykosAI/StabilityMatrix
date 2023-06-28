using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StabilityMatrix.Models.FileInterfaces;

public class DirectoryPath : FileSystemPath, IPathObject
{
    private DirectoryInfo? _info;
    // ReSharper disable once MemberCanBePrivate.Global
    public DirectoryInfo Info => _info ??= new DirectoryInfo(FullPath);

    public bool IsSymbolicLink
    {
        get
        {
            Info.Refresh();
            return Info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
    }
    
    public bool Exists => Info.Exists;

    public DirectoryPath(string path) : base(path)
    {
    }
    
    public DirectoryPath(FileSystemPath path) : base(path)
    {
    }
    
    public DirectoryPath(params string[] paths) : base(paths)
    {
    }

    public long GetSize()
    {
        ulong size = 1 + 2;
        Info.Refresh();
        return Info.EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(file => file.Length);
    }
    
    public long GetSize(bool includeSymbolicLinks)
    {
        if (includeSymbolicLinks) return GetSize();
        
        Info.Refresh();
        var files = Info.GetFiles()
            .Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Sum(file => file.Length);
        var subDirs = Info.GetDirectories()
            .Where(dir => !dir.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Sum(dir => dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length));
        return files + subDirs;
    }

    public Task<long> GetSizeAsync(bool includeSymbolicLinks)
    {
        return Task.Run(() => GetSize(includeSymbolicLinks));
    }
    
    /// <summary> Creates the directory. </summary>
    public void Create() => Directory.CreateDirectory(FullPath);
    
    /// <summary> Deletes the directory </summary>
    public void Delete() => Directory.Delete(FullPath);
    
    /// <summary> Deletes the directory. </summary>
    public void Delete(bool recursive) => Directory.Delete(FullPath, recursive);

    /// <summary> Deletes the directory asynchronously. </summary>
    public Task DeleteAsync(bool recursive) => Task.Run(() => Delete(recursive));
    
    // DirectoryPath + DirectoryPath = DirectoryPath
    public static DirectoryPath operator +(DirectoryPath path, DirectoryPath other) => new(Path.Combine(path, other.FullPath));
    
    // DirectoryPath + FilePath = FilePath
    public static FilePath operator +(DirectoryPath path, FilePath other) => new(Path.Combine(path, other.FullPath));
    
    // DirectoryPath + string = string
    public static string operator +(DirectoryPath path, string other) => Path.Combine(path, other);
    
    // Implicit conversions to and from string
    public static implicit operator string(DirectoryPath path) => path.FullPath;
    public static implicit operator DirectoryPath(string path) => new(path);
}
