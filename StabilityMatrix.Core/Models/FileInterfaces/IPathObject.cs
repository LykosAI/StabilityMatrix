namespace StabilityMatrix.Core.Models.FileInterfaces;

public interface IPathObject
{
    /// <summary> Full path of the file system object. </summary>
    string FullPath { get; }

    /// <summary> Info of the file system object. </summary>
    FileSystemInfo Info { get; }

    /// <summary> Name of the file system object. </summary>
    string Name { get; }

    /// <summary> Whether the file system object is a symbolic link or junction. </summary>
    bool IsSymbolicLink { get; }

    /// <summary> Gets the size of the file system object. </summary>
    long GetSize();

    /// <summary> Gets the size of the file system object asynchronously. </summary>
    Task<long> GetSizeAsync() => Task.Run(GetSize);

    /// <summary> Whether the file system object exists. </summary>
    bool Exists { get; }

    /// <summary> Deletes the file system object </summary>
    void Delete();

    /// <summary> Deletes the file system object asynchronously. </summary>
    public Task DeleteAsync() => Task.Run(Delete);
}
