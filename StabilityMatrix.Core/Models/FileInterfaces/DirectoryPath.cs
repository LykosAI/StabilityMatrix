using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Core.Models.FileInterfaces;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class DirectoryPath : FileSystemPath, IPathObject
{
    private DirectoryInfo? info;
    // ReSharper disable once MemberCanBePrivate.Global
    public DirectoryInfo Info => info ??= new DirectoryInfo(FullPath);

    public bool IsSymbolicLink
    {
        get
        {
            Info.Refresh();
            return Info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
    }
    
    /// <summary>
    /// Gets a value indicating whether the directory exists.
    /// </summary>
    public bool Exists => Info.Exists;
    
    /// <inheritdoc/>
    public string Name => Info.Name;
    
    /// <summary>
    /// Get the parent directory.
    /// </summary>
    public DirectoryPath? Parent => Info.Parent == null 
        ? null : new DirectoryPath(Info.Parent);

    public DirectoryPath(string path) : base(path)
    {
    }
    
    public DirectoryPath(FileSystemPath path) : base(path)
    {
    }
    
    public DirectoryPath(DirectoryInfo info) : base(info.FullName)
    {
        // Additionally set the info field
        this.info = info;
    }
    
    public DirectoryPath(params string[] paths) : base(paths)
    {
    }

    /// <inheritdoc />
    public long GetSize()
    {
        Info.Refresh();
        return Info.EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(file => file.Length);
    }
    
    /// <summary>
    /// Gets the size of the directory.
    /// </summary>
    /// <param name="includeSymbolicLinks">
    /// Whether to include files and subdirectories that are symbolic links / reparse points.
    /// </param>
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

    /// <summary>
    /// Gets the size of the directory asynchronously.
    /// </summary>
    /// <param name="includeSymbolicLinks">
    /// Whether to include files and subdirectories that are symbolic links / reparse points.
    /// </param>
    public Task<long> GetSizeAsync(bool includeSymbolicLinks)
    {
        return Task.Run(() => GetSize(includeSymbolicLinks));
    }
    
    /// <summary>
    /// Creates the directory.
    /// </summary>
    public void Create() => Directory.CreateDirectory(FullPath);

    /// <summary>
    /// Deletes the directory.
    /// </summary>
    public void Delete() => Directory.Delete(FullPath);
    
    /// <summary> Deletes the directory asynchronously. </summary>
    public Task DeleteAsync() => Task.Run(Delete);

    /// <summary>
    /// Deletes the directory.
    /// </summary>
    /// <param name="recursive">Whether to delete subdirectories and files.</param>
    public void Delete(bool recursive) => Info.Delete(recursive);

    /// <summary>
    /// Deletes the directory asynchronously.
    /// </summary>
    public Task DeleteAsync(bool recursive) => Task.Run(() => Delete(recursive));
    
    /// <summary>
    /// Join with other paths to form a new directory path.
    /// </summary>
    public DirectoryPath JoinDir(params DirectoryPath[] paths) => 
        new(Path.Combine(FullPath, Path.Combine(paths.Select(path => path.FullPath).ToArray())));
    
    /// <summary>
    /// Join with other paths to form a new file path.
    /// </summary>
    public FilePath JoinFile(params FilePath[] paths) => 
        new(Path.Combine(FullPath, Path.Combine(paths.Select(path => path.FullPath).ToArray())));

    public override string ToString() => FullPath;

    // DirectoryPath + DirectoryPath = DirectoryPath
    public static DirectoryPath operator +(DirectoryPath path, DirectoryPath other) => new(Path.Combine(path, other.FullPath));
    
    // DirectoryPath + FilePath = FilePath
    public static FilePath operator +(DirectoryPath path, FilePath other) => new(Path.Combine(path, other.FullPath));
    
    // DirectoryPath + FileInfo = FilePath
    public static FilePath operator +(DirectoryPath path, FileInfo other) => new(Path.Combine(path, other.FullName));
    
    // DirectoryPath + string = string
    public static string operator +(DirectoryPath path, string other) => Path.Combine(path, other);
    
    // Implicit conversions to and from string
    public static implicit operator string(DirectoryPath path) => path.FullPath;
    public static implicit operator DirectoryPath(string path) => new(path);
    
    // Implicit conversions to and from DirectoryInfo
    public static implicit operator DirectoryInfo(DirectoryPath path) => path.Info;
    public static implicit operator DirectoryPath(DirectoryInfo path) => new(path);
}
