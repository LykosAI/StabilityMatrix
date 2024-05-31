using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using StabilityMatrix.Native.Windows.Interop;

namespace StabilityMatrix.Native.Windows.FileOperations;

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal partial class FileOperationWrapper : IDisposable
{
    private bool _disposed;
    private readonly IFileOperation _fileOperation;
    private readonly IFileOperationProgressSink? _callbackSink;
    private readonly uint _sinkCookie;

    [PublicAPI]
    public FileOperationWrapper()
        : this(null) { }

    public FileOperationWrapper(IFileOperationProgressSink? callbackSink)
        : this(callbackSink, IntPtr.Zero) { }

    public FileOperationWrapper(IFileOperationProgressSink? callbackSink, IntPtr ownerHandle)
    {
        _callbackSink = callbackSink;
        _fileOperation =
            (IFileOperation?)Activator.CreateInstance(FileOperationType)
            ?? throw new NullReferenceException("Failed to create FileOperation instance.");

        if (_callbackSink != null)
            _sinkCookie = _fileOperation.Advise(_callbackSink);
        if (ownerHandle != IntPtr.Zero)
            _fileOperation.SetOwnerWindow((uint)ownerHandle);
    }

    public void SetOperationFlags(FileOperationFlags operationFlags)
    {
        _fileOperation.SetOperationFlags(operationFlags);
    }

    [PublicAPI]
    public void CopyItem(string source, string destination, string newName)
    {
        ThrowIfDisposed();
        using var sourceItem = CreateShellItem(source);
        using var destinationItem = CreateShellItem(destination);
        _fileOperation.CopyItem(sourceItem.Item, destinationItem.Item, newName, null);
    }

    [PublicAPI]
    public void MoveItem(string source, string destination, string newName)
    {
        ThrowIfDisposed();
        using var sourceItem = CreateShellItem(source);
        using var destinationItem = CreateShellItem(destination);
        _fileOperation.MoveItem(sourceItem.Item, destinationItem.Item, newName, null);
    }

    [PublicAPI]
    public void RenameItem(string source, string newName)
    {
        ThrowIfDisposed();
        using var sourceItem = CreateShellItem(source);
        _fileOperation.RenameItem(sourceItem.Item, newName, null);
    }

    public void DeleteItem(string source)
    {
        ThrowIfDisposed();
        using var sourceItem = CreateShellItem(source);
        _fileOperation.DeleteItem(sourceItem.Item, null);
    }

    /*public void DeleteItems(params string[] sources)
    {
        ThrowIfDisposed();
        using var sourceItems = CreateShellItemArray(sources);
        _fileOperation.DeleteItems(sourceItems.Item);
    }*/

    public void DeleteItems(string[] sources)
    {
        ThrowIfDisposed();

        var pidlArray = new IntPtr[sources.Length];

        try
        {
            // Convert paths to PIDLs
            for (var i = 0; i < sources.Length; i++)
            {
                pidlArray[i] = ILCreateFromPathW(sources[i]);
                if (pidlArray[i] == IntPtr.Zero)
                    throw new Exception($"Failed to create PIDL for path: {sources[i]}");
            }

            // Create ShellItemArray from PIDLs
            var shellItemArray = SHCreateShellItemArrayFromIDLists((uint)sources.Length, pidlArray);

            // Use the IFileOperation interface to delete items
            _fileOperation.DeleteItems(shellItemArray);
        }
        finally
        {
            // Free PIDLs
            foreach (var pidl in pidlArray)
            {
                if (pidl != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pidl);
                }
            }
        }
    }

    [PublicAPI]
    public void NewItem(string folderName, string name, FileAttributes attrs)
    {
        ThrowIfDisposed();
        using var folderItem = CreateShellItem(folderName);
        _fileOperation.NewItem(folderItem.Item, attrs, name, string.Empty, _callbackSink);
    }

    public void PerformOperations()
    {
        ThrowIfDisposed();
        _fileOperation.PerformOperations();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            if (_callbackSink != null)
            {
                _fileOperation.Unadvise(_sinkCookie);
            }

            Marshal.FinalReleaseComObject(_fileOperation);
        }
    }

    private static ComReleaser<IShellItem> CreateShellItem(string path)
    {
        // Normalize path slashes
        path = path.Replace('/', '\\');

        return new ComReleaser<IShellItem>(
            (IShellItem)SHCreateItemFromParsingName(path, IntPtr.Zero, ref _shellItemGuid)
        );
    }

    private static ComReleaser<IShellItemArray> CreateShellItemArray(params string[] paths)
    {
        var pidls = new IntPtr[paths.Length];

        try
        {
            for (var i = 0; i < paths.Length; i++)
            {
                if (SHParseDisplayName(paths[i], IntPtr.Zero, out var pidl, 0, out _) != 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                pidls[i] = pidl;
            }

            return new ComReleaser<IShellItemArray>(
                SHCreateShellItemArrayFromIDLists((uint)pidls.Length, pidls)
            );
        }
        finally
        {
            foreach (var pidl in pidls)
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }
    }

    [LibraryImport("shell32.dll", SetLastError = true)]
    private static partial int SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        IntPtr pbc, // IBindCtx
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut
    );

    [LibraryImport("shell32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr ILCreateFromPathW(string pszPath);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode, PreserveSig = false)]
    [return: MarshalAs(UnmanagedType.Interface)]
    private static extern object SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc, // IBindCtx
        ref Guid riid
    );

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode, PreserveSig = false)]
    [return: MarshalAs(UnmanagedType.Interface)]
    private static extern IShellItemArray SHCreateShellItemArrayFromIDLists(
        uint cidl,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStruct)] IntPtr[] rgpidl
    );

    private static readonly Guid ClsidFileOperation = new("3ad05575-8857-4850-9277-11b85bdb8e09");
    private static readonly Type FileOperationType =
        Type.GetTypeFromCLSID(ClsidFileOperation)
        ?? throw new NullReferenceException("Failed to get FileOperation type from CLSID");
    private static Guid _shellItemGuid = typeof(IShellItem).GUID;
}
