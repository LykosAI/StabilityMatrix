using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.FileInterfaces;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[JsonConverter(typeof(StringJsonConverter<DirectoryPath>))]
public class DirectoryPath : FileSystemPath, IPathObject, IEnumerable<FileSystemPath>
{
    private DirectoryInfo? info;

    // ReSharper disable once MemberCanBePrivate.Global
    [JsonIgnore]
    public DirectoryInfo Info => info ??= new DirectoryInfo(FullPath);

    [JsonIgnore]
    public bool IsSymbolicLink
    {
        get
        {
            Info.Refresh();
            return Info.Exists && Info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the directory exists.
    /// </summary>
    [JsonIgnore]
    public bool Exists => Info.Exists;

    /// <inheritdoc/>
    [JsonIgnore]
    public string Name => Info.Name;

    /// <summary>
    /// Get the parent directory.
    /// </summary>
    [JsonIgnore]
    public DirectoryPath? Parent => Info.Parent == null ? null : new DirectoryPath(Info.Parent);

    public DirectoryPath(string path)
        : base(path) { }

    public DirectoryPath(FileSystemPath path)
        : base(path) { }

    public DirectoryPath(DirectoryInfo info)
        : base(info.FullName)
    {
        // Additionally set the info field
        this.info = info;
    }

    public DirectoryPath(params string[] paths)
        : base(paths) { }

    /// <inheritdoc />
    public long GetSize()
    {
        Info.Refresh();
        return Info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
    }

    /// <summary>
    /// Gets the size of the directory.
    /// </summary>
    /// <param name="includeSymbolicLinks">
    /// Whether to include files and subdirectories that are symbolic links / reparse points.
    /// </param>
    public long GetSize(bool includeSymbolicLinks)
    {
        if (includeSymbolicLinks)
            return GetSize();

        Info.Refresh();
        var files = Info.GetFiles()
            .Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Sum(file => file.Length);
        var subDirs = Info.GetDirectories()
            .Where(dir => !dir.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Sum(
                dir => dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length)
            );
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
    public void Delete() => Info.Delete();

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

    /// <summary>
    /// Returns an enumerable collection of files that matches
    /// a specified search pattern and search subdirectory option.
    /// </summary>
    public IEnumerable<FilePath> EnumerateFiles(
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    ) => Info.EnumerateFiles(searchPattern, searchOption).Select(file => new FilePath(file));

    /// <summary>
    /// Returns an enumerable collection of directories that matches
    /// a specified search pattern and search subdirectory option.
    /// </summary>
    public IEnumerable<DirectoryPath> EnumerateDirectories(
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    ) =>
        Info.EnumerateDirectories(searchPattern, searchOption)
            .Select(directory => new DirectoryPath(directory));

    public override string ToString() => FullPath;

    /// <inheritdoc />
    public IEnumerator<FileSystemPath> GetEnumerator()
    {
        return Info.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly)
            .Select<FileSystemInfo, FileSystemPath>(
                fsInfo =>
                    fsInfo switch
                    {
                        FileInfo file => new FilePath(file),
                        DirectoryInfo directory => new DirectoryPath(directory),
                        _ => throw new InvalidOperationException("Unknown file system info type")
                    }
            )
            .GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // DirectoryPath + DirectoryPath = DirectoryPath
    public static DirectoryPath operator +(DirectoryPath path, DirectoryPath other) =>
        new(Path.Combine(path, other.FullPath));

    // DirectoryPath + FilePath = FilePath
    public static FilePath operator +(DirectoryPath path, FilePath other) =>
        new(Path.Combine(path, other.FullPath));

    // DirectoryPath + FileInfo = FilePath
    public static FilePath operator +(DirectoryPath path, FileInfo other) =>
        new(Path.Combine(path, other.FullName));

    // DirectoryPath + string = string
    public static string operator +(DirectoryPath path, string other) => Path.Combine(path, other);

    // Implicit conversions to and from string
    public static implicit operator string(DirectoryPath path) => path.FullPath;

    public static implicit operator DirectoryPath(string path) => new(path);

    // Implicit conversions to and from DirectoryInfo
    public static implicit operator DirectoryInfo(DirectoryPath path) => path.Info;

    public static implicit operator DirectoryPath(DirectoryInfo path) => new(path);
}
