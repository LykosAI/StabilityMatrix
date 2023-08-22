using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.FileInterfaces;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[JsonConverter(typeof(StringJsonConverter<FilePath>))]
public class FilePath : FileSystemPath, IPathObject
{
    private FileInfo? _info;
    // ReSharper disable once MemberCanBePrivate.Global
    [JsonIgnore]
    public FileInfo Info => _info ??= new FileInfo(FullPath);

    [JsonIgnore]
    public bool IsSymbolicLink
    {
        get
        {
            Info.Refresh();
            return Info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
    }
    
    [JsonIgnore]
    public bool Exists => Info.Exists;
    
    [JsonIgnore]
    public string Name => Info.Name;

    /// <summary>
    /// Get the directory of the file.
    /// </summary>
    [JsonIgnore]
    public DirectoryPath? Directory
    {
        get
        {
            try
            {
                return Info.Directory == null ? null
                    : new DirectoryPath(Info.Directory);
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }
    }

    public FilePath(string path) : base(path)
    {
    }
    
    public FilePath(FileSystemPath path) : base(path)
    {
    }
    
    public FilePath(params string[] paths) : base(paths)
    {
    }

    public long GetSize()
    {
        Info.Refresh();
        return Info.Length;
    }
    
    public long GetSize(bool includeSymbolicLinks)
    {
        if (!includeSymbolicLinks && IsSymbolicLink) return 0;
        return GetSize();
    }

    public Task<long> GetSizeAsync(bool includeSymbolicLinks)
    {
        return Task.Run(() => GetSize(includeSymbolicLinks));
    }
    
    /// <summary> Creates an empty file. </summary>
    public void Create() => File.Create(FullPath).Close();
    
    /// <summary> Deletes the file </summary>
    public void Delete() => File.Delete(FullPath);
    
    // Methods specific to files
    
    /// <summary> Read text </summary>
    public string ReadAllText() => File.ReadAllText(FullPath);
    
    /// <summary> Read text asynchronously </summary>
    public Task<string> ReadAllTextAsync(CancellationToken ct = default)
    {
        return File.ReadAllTextAsync(FullPath, ct);
    }
    
    /// <summary> Write text </summary>
    public void WriteAllText(string text) => File.WriteAllText(FullPath, text, Encoding.UTF8);
    
    /// <summary> Write text asynchronously </summary>
    public Task WriteAllTextAsync(string text, CancellationToken ct = default)
    {
        return File.WriteAllTextAsync(FullPath, text, Encoding.UTF8, ct);
    }
    
    /// <summary> Read bytes </summary>
    public byte[] ReadAllBytes() => File.ReadAllBytes(FullPath);
    
    /// <summary> Read bytes asynchronously </summary>
    public Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default)
    {
        return File.ReadAllBytesAsync(FullPath, ct);
    }
    
    /// <summary> Write bytes </summary>
    public void WriteAllBytes(byte[] bytes) => File.WriteAllBytes(FullPath, bytes);
    
    /// <summary> Write bytes asynchronously </summary>
    public Task WriteAllBytesAsync(byte[] bytes, CancellationToken ct = default)
    {
        return File.WriteAllBytesAsync(FullPath, bytes, ct);
    }
    
    /// <summary>
    /// Move the file to a directory.
    /// </summary>
    public FilePath MoveTo(FilePath destinationFile)
    {
        Info.MoveTo(destinationFile.FullPath, true);
        // Return the new path
        return destinationFile;
    }
    
    /// <summary>
    /// Move the file to a directory.
    /// </summary>
    public async Task<FilePath> MoveToAsync(DirectoryPath directory)
    {
        await Task.Run(() => Info.MoveTo(directory.FullPath)).ConfigureAwait(false);
        // Return the new path
        return directory.JoinFile(this);
    }
    
    /// <summary>
    /// Move the file to a target path.
    /// </summary>
    public async Task<FilePath> MoveToAsync(FilePath destinationFile)
    {
        await Task.Run(() => Info.MoveTo(destinationFile.FullPath)).ConfigureAwait(false);
        // Return the new path
        return destinationFile;
    }
    
    /// <summary>
    /// Copy the file to a target path.
    /// </summary>
    public FilePath CopyTo(FilePath destinationFile, bool overwrite = false)
    {
        Info.CopyTo(destinationFile.FullPath, overwrite);
        // Return the new path
        return destinationFile;
    }

    // Implicit conversions to and from string
    public static implicit operator string(FilePath path) => path.FullPath;
    public static implicit operator FilePath(string path) => new(path);
}
