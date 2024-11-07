using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.FileInterfaces;

[PublicAPI]
[Localizable(false)]
[JsonConverter(typeof(StringJsonConverter<FilePath>))]
public partial class FilePath : FileSystemPath, IPathObject
{
    private FileInfo? _info;

    [JsonIgnore]
    public FileInfo Info => _info ??= new FileInfo(FullPath);

    [JsonIgnore]
    FileSystemInfo IPathObject.Info => Info;

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

    [JsonIgnore]
    public string NameWithoutExtension => Path.GetFileNameWithoutExtension(Info.Name);

    /// <inheritdoc cref="FileInfo.Extension"/>
    [JsonIgnore]
    public string Extension => Info.Extension;

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
                return Info.Directory == null ? null : new DirectoryPath(Info.Directory);
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }
    }

    public FilePath([Localizable(false)] string path)
        : base(path) { }

    public FilePath(FileInfo fileInfo)
        : base(fileInfo.FullName)
    {
        _info = fileInfo;
    }

    public FilePath(FileSystemPath path)
        : base(path) { }

    public FilePath([Localizable(false)] params string[] paths)
        : base(paths) { }

    public FilePath RelativeTo(DirectoryPath path)
    {
        return new FilePath(Path.GetRelativePath(path.FullPath, FullPath));
    }

    public long GetSize()
    {
        Info.Refresh();
        return Info.Length;
    }

    public long GetSize(bool includeSymbolicLinks)
    {
        if (!includeSymbolicLinks && IsSymbolicLink)
            return 0;
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

    /// <summary> Deletes the file asynchronously </summary>
    public Task DeleteAsync(CancellationToken ct = default)
    {
        return Task.Run(() => File.Delete(FullPath), ct);
    }

    // Methods specific to files

    /// <summary> Read text </summary>
    public string ReadAllText() => File.ReadAllText(FullPath);

    /// <summary> Read text asynchronously </summary>
    public Task<string> ReadAllTextAsync(CancellationToken ct = default)
    {
        return File.ReadAllTextAsync(FullPath, ct);
    }

    /// <summary> Write text </summary>
    public void WriteAllText(string text, Encoding? encoding = null) =>
        File.WriteAllText(FullPath, text, encoding ?? new UTF8Encoding(false));

    /// <summary> Write text asynchronously </summary>
    public Task WriteAllTextAsync(string text, CancellationToken ct = default, Encoding? encoding = null)
    {
        return File.WriteAllTextAsync(FullPath, text, encoding ?? new UTF8Encoding(false), ct);
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
    /// Rename the file.
    /// </summary>
    public FilePath Rename([Localizable(false)] string fileName)
    {
        if (Path.GetDirectoryName(FullPath) is { } directory && !string.IsNullOrWhiteSpace(directory))
        {
            var target = Path.Combine(directory, fileName);
            Info.MoveTo(target, true);
            return new FilePath(target);
        }

        throw new InvalidOperationException("Cannot rename a file path that is empty or has no directory");
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
    public async Task<FilePath> MoveToDirectoryAsync(DirectoryPath directory)
    {
        await Task.Run(() => Info.MoveTo(directory.JoinFile(Name), true)).ConfigureAwait(false);
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
    /// Move the file to a target path with auto increment if the file already exists.
    /// </summary>
    /// <returns>The new path, possibly with incremented file name</returns>
    public async Task<FilePath> MoveToWithIncrementAsync(FilePath destinationFile, int maxTries = 100)
    {
        await Task.Yield();

        var targetFile = destinationFile;

        for (var i = 1; i < maxTries; i++)
        {
            if (!targetFile.Exists)
            {
                return await MoveToAsync(targetFile).ConfigureAwait(false);
            }

            targetFile = destinationFile.WithName(
                destinationFile.NameWithoutExtension + $" ({i})" + destinationFile.Extension
            );
        }

        throw new IOException($"Could not move file to {destinationFile} because it already exists.");
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

    /// <summary>
    /// Copy the file to a target path asynchronously.
    /// </summary>
    public async Task<FilePath> CopyToAsync(FilePath destinationFile, bool overwrite = false)
    {
        await using var sourceStream = Info.OpenRead();
        await using var destinationStream = destinationFile.Info.OpenWrite();

        await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);

        // Return the new path
        return destinationFile;
    }

    /// <summary>
    /// Copy the file to a target path asynchronously with a specified the file share mode.
    /// </summary>
    public async Task<FilePath> CopyToAsync(
        FilePath destinationFile,
        FileShare sourceShare,
        bool overwrite = false
    )
    {
        await using var sourceStream = Info.Open(FileMode.Open, FileAccess.Read, sourceShare);
        await using var destinationStream = destinationFile.Info.OpenWrite();

        await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);

        // Return the new path
        return destinationFile;
    }

    // Implicit conversions to and from string
    public static implicit operator string(FilePath path) => path.FullPath;

    public static implicit operator FilePath([Localizable(false)] string path) => new(path);
}
