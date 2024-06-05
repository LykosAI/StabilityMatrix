using JetBrains.Annotations;
using StabilityMatrix.Native.Abstractions;
using StabilityMatrix.Native.Windows.FileOperations;
using StabilityMatrix.Native.Windows.Interop;

namespace StabilityMatrix.Native.Windows;

[PublicAPI]
public class NativeRecycleBinProvider : INativeRecycleBinProvider
{
    /// <inheritdoc />
    public void MoveFileToRecycleBin(string path, NativeFileOperationFlags flags = default)
    {
        using var fo = new FileOperationWrapper();

        var fileOperationFlags = default(uint);
        flags.ToWindowsFileOperationFlags(ref fileOperationFlags);

        fo.SetOperationFlags(
            (FileOperationFlags)fileOperationFlags | FileOperationFlags.FOFX_RECYCLEONDELETE
        );
        fo.DeleteItem(path);
        fo.PerformOperations();
    }

    /// <inheritdoc />
    public Task MoveFileToRecycleBinAsync(string path, NativeFileOperationFlags flags = default)
    {
        return Task.Run(() => MoveFileToRecycleBin(path, flags));
    }

    /// <inheritdoc />
    public void MoveFilesToRecycleBin(IEnumerable<string> paths, NativeFileOperationFlags flags = default)
    {
        using var fo = new FileOperationWrapper();

        var fileOperationFlags = default(uint);
        flags.ToWindowsFileOperationFlags(ref fileOperationFlags);

        fo.SetOperationFlags(
            (FileOperationFlags)fileOperationFlags | FileOperationFlags.FOFX_RECYCLEONDELETE
        );
        fo.DeleteItems(paths.ToArray());
        fo.PerformOperations();
    }

    /// <inheritdoc />
    public Task MoveFilesToRecycleBinAsync(
        IEnumerable<string> paths,
        NativeFileOperationFlags flags = default
    )
    {
        return Task.Run(() => MoveFilesToRecycleBin(paths, flags));
    }

    /// <inheritdoc />
    public void MoveDirectoryToRecycleBin(string path, NativeFileOperationFlags flags = default)
    {
        using var fo = new FileOperationWrapper();

        var fileOperationFlags = default(uint);
        flags.ToWindowsFileOperationFlags(ref fileOperationFlags);

        fo.SetOperationFlags(
            (FileOperationFlags)fileOperationFlags | FileOperationFlags.FOFX_RECYCLEONDELETE
        );
        fo.DeleteItem(path);
        fo.PerformOperations();
    }

    /// <inheritdoc />
    public Task MoveDirectoryToRecycleBinAsync(string path, NativeFileOperationFlags flags = default)
    {
        return Task.Run(() => MoveDirectoryToRecycleBin(path, flags));
    }

    /// <inheritdoc />
    public void MoveDirectoriesToRecycleBin(
        IEnumerable<string> paths,
        NativeFileOperationFlags flags = default
    )
    {
        using var fo = new FileOperationWrapper();

        var fileOperationFlags = default(uint);
        flags.ToWindowsFileOperationFlags(ref fileOperationFlags);

        fo.SetOperationFlags(
            (FileOperationFlags)fileOperationFlags | FileOperationFlags.FOFX_RECYCLEONDELETE
        );
        fo.DeleteItems(paths.ToArray());
        fo.PerformOperations();
    }

    /// <inheritdoc />
    public Task MoveDirectoriesToRecycleBinAsync(
        IEnumerable<string> paths,
        NativeFileOperationFlags flags = default
    )
    {
        return Task.Run(() => MoveDirectoriesToRecycleBin(paths, flags));
    }
}
