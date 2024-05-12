using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Helper;

[SupportedOSPlatform("windows")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static partial class WindowsFileOperations
{
    public enum HRESULT : uint
    {
        S_OK = 0x00000000,
        S_FALSE = 0x00000001,
        E_ABORT = 0x80004004,
        E_FAIL = 0x80004005,
        E_NOINTERFACE = 0x80004002,
        E_NOTIMPLEMENTED = 0x80004001,
        E_POINTER = 0x80004003,
        E_UNEXPECTED = 0x8000FFFF,
        E_ACCESSDENIED = 0x80070005,
        E_HANDLE = 0x80070006,
        E_INVALIDARG = 0x80070057,
        E_OUTOFMEMORY = 0x8007000E,
    }

    /// <summary>
    /// Possible flags for the SHFileOperation method.
    /// </summary>
    [Flags]
    public enum FileOperationFlags : uint
    {
        FOF_MULTIDESTFILES = 0x0001,
        FOF_CONFIRMMOUSE = 0x0002,
        FOF_WANTMAPPINGHANDLE = 0x0020, // Fill in SHFILEOPSTRUCT.hNameMappings
        FOF_FILESONLY = 0x0080, // on *.*, do only files
        FOF_NOCONFIRMMKDIR = 0x0200, // don't confirm making any needed dirs
        FOF_NOCOPYSECURITYATTRIBS = 0x0800, // dont copy NT file Security Attributes
        FOF_NORECURSION = 0x1000, // don't recurse into directories.
        FOF_NO_CONNECTED_ELEMENTS = 0x2000, // don't operate on connected file elements.
        FOF_NORECURSEREPARSE = 0x8000, // treat reparse points as objects, not containers

        /// <summary>
        /// Do not show a dialog during the process
        /// </summary>
        FOF_SILENT = 0x0004,

        FOF_RENAMEONCOLLISION = 0x0008,

        /// <summary>
        /// Do not ask the user to confirm selection
        /// </summary>
        FOF_NOCONFIRMATION = 0x0010,

        /// <summary>
        /// Delete the file to the recycle bin.  (Required flag to send a file to the bin
        /// </summary>
        FOF_ALLOWUNDO = 0x0040,

        /// <summary>
        /// Do not show the names of the files or folders that are being recycled.
        /// </summary>
        FOF_SIMPLEPROGRESS = 0x0100,

        /// <summary>
        /// Surpress errors, if any occur during the process.
        /// </summary>
        FOF_NOERRORUI = 0x0400,

        /// <summary>
        /// Warn if files are too big to fit in the recycle bin and will need
        /// to be deleted completely.
        /// </summary>
        FOF_WANTNUKEWARNING = 0x4000,

        FOFX_ADDUNDORECORD = 0x20000000,

        FOFX_NOSKIPJUNCTIONS = 0x00010000,

        FOFX_PREFERHARDLINK = 0x00020000,

        FOFX_SHOWELEVATIONPROMPT = 0x00040000,

        FOFX_EARLYFAILURE = 0x00100000,

        FOFX_PRESERVEFILEEXTENSIONS = 0x00200000,

        FOFX_KEEPNEWERFILE = 0x00400000,

        FOFX_NOCOPYHOOKS = 0x00800000,

        FOFX_NOMINIMIZEBOX = 0x01000000,

        FOFX_MOVEACLSACROSSVOLUMES = 0x02000000,

        FOFX_DONTDISPLAYSOURCEPATH = 0x04000000,

        FOFX_DONTDISPLAYDESTPATH = 0x08000000,

        FOFX_RECYCLEONDELETE = 0x00080000,

        FOFX_REQUIREELEVATION = 0x10000000,

        FOFX_COPYASDOWNLOAD = 0x40000000,

        FOFX_DONTDISPLAYLOCATIONS = 0x80000000,
    }

    /// <summary>
    /// File Operation Function Type for SHFileOperation
    /// </summary>
    public enum FileOperationType : uint
    {
        /// <summary>
        /// Move the objects
        /// </summary>
        FO_MOVE = 0x0001,

        /// <summary>
        /// Copy the objects
        /// </summary>
        FO_COPY = 0x0002,

        /// <summary>
        /// Delete (or recycle) the objects
        /// </summary>
        FO_DELETE = 0x0003,

        /// <summary>
        /// Rename the object(s)
        /// </summary>
        FO_RENAME = 0x0004,
    }

    public enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0x00000000,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000
    }

    internal sealed class ComReleaser<T> : IDisposable
        where T : class
    {
        public ComReleaser(T obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (!Marshal.IsComObject(obj))
                throw new ArgumentOutOfRangeException(nameof(obj));
            Item = obj;
        }

        public T? Item { get; private set; }

        public void Dispose()
        {
            if (Item != null)
            {
                Marshal.FinalReleaseComObject(Item);
                Item = null;
            }
        }
    }

    [GeneratedComInterface]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IShellItem
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object BindToHandler(
            IntPtr pbc, // IBindCTX
            ref Guid bhid,
            ref Guid riid
        );

        IShellItem GetParent();

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName(SIGDN sigdnName);

        uint GetAttributes(uint sfgaoMask);

        int Compare(IShellItem psi, uint hint);
    }

    [GeneratedComInterface]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IShellItemArray
    {
        // uint BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out object ppvOut);

        IShellItem GetItemAt(uint dwIndex);

        uint GetCount();
    }

    [GeneratedComInterface]
    [Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IFileOperationProgressSink
    {
        void StartOperations();
        void FinishOperations(uint hrResult);

        void PreRenameItem(
            uint dwFlags,
            IShellItem psiItem,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName
        );
        void PostRenameItem(
            uint dwFlags,
            IShellItem psiItem,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName,
            uint hrRename,
            IShellItem psiNewlyCreated
        );

        void PreMoveItem(
            uint dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName
        );
        void PostMoveItem(
            uint dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName,
            uint hrMove,
            IShellItem psiNewlyCreated
        );

        void PreCopyItem(
            uint dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName
        );
        void PostCopyItem(
            uint dwFlags,
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName,
            uint hrCopy,
            IShellItem psiNewlyCreated
        );

        void PreDeleteItem(uint dwFlags, IShellItem psiItem);
        void PostDeleteItem(uint dwFlags, IShellItem psiItem, uint hrDelete, IShellItem psiNewlyCreated);

        void PreNewItem(
            uint dwFlags,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName
        );
        void PostNewItem(
            uint dwFlags,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName,
            [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName,
            uint dwFileAttributes,
            uint hrNew,
            IShellItem psiNewItem
        );

        void UpdateProgress(uint iWorkTotal, uint iWorkSoFar);

        void ResetTimer();
        void PauseTimer();
        void ResumeTimer();
    }

    internal partial class FileOperation : IDisposable
    {
        private bool _disposed;
        private readonly IFileOperation _fileOperation;
        private readonly IFileOperationProgressSink _callbackSink;
        private readonly uint _sinkCookie;

        [PublicAPI]
        public FileOperation()
            : this(null) { }

        public FileOperation(IFileOperationProgressSink callbackSink)
            : this(callbackSink, IntPtr.Zero) { }

        public FileOperation(IFileOperationProgressSink callbackSink, IntPtr ownerHandle)
        {
            _callbackSink = callbackSink;
            _fileOperation = (IFileOperation)Activator.CreateInstance(FileOperationType);

            _fileOperation.SetOperationFlags(FileOperationFlags.FOF_NOCONFIRMMKDIR);
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

        public void DeleteItems(params string[] sources)
        {
            ThrowIfDisposed();
            using var sourceItems = CreateShellItemArray(sources);
            _fileOperation.DeleteItems(sourceItems.Item);
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
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_callbackSink != null)
                    _fileOperation.Unadvise(_sinkCookie);
                Marshal.FinalReleaseComObject(_fileOperation);
            }
        }

        private static ComReleaser<IShellItem> CreateShellItem(string path)
        {
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
                        ThrowLastWin32Error("Failed to parse display name.");
                    }

                    pidls[i] = pidl;
                }

                return new ComReleaser<IShellItemArray>(
                    (IShellItemArray)SHCreateShellItemArrayFromIDLists((uint)pidls.Length, pidls)
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

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern object SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc, // IBindCtx
            ref Guid riid
        );

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern object SHCreateShellItemArrayFromIDLists(
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStruct)] IntPtr[] rgpidl
        );

        private static readonly Guid ClsidFileOperation = new("3ad05575-8857-4850-9277-11b85bdb8e09");
        private static readonly Type FileOperationType = Type.GetTypeFromCLSID(ClsidFileOperation);
        private static Guid _shellItemGuid = typeof(IShellItem).GUID;
    }

    [GeneratedComInterface]
    [Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IFileOperation
    {
        uint Advise(IFileOperationProgressSink pfops);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(FileOperationFlags dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog([MarshalAs(UnmanagedType.Interface)] object popd);
        void SetProperties([MarshalAs(UnmanagedType.Interface)] object pproparray);
        void SetOwnerWindow(uint hwndParent);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        void RenameItem(
            IShellItem psiItem,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName,
            IFileOperationProgressSink pfopsItem
        );
        void RenameItems(
            [MarshalAs(UnmanagedType.Interface)] object pUnkItems,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName
        );
        void MoveItem(
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszNewName,
            IFileOperationProgressSink pfopsItem
        );
        void MoveItems(
            [MarshalAs(UnmanagedType.Interface)] object punkItems,
            IShellItem psiDestinationFolder
        );
        void CopyItem(
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName,
            IFileOperationProgressSink pfopsItem
        );
        void CopyItems(
            [MarshalAs(UnmanagedType.Interface)] object punkItems,
            IShellItem psiDestinationFolder
        );
        void DeleteItem(IShellItem psiItem, IFileOperationProgressSink pfopsItem);
        void DeleteItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        uint NewItem(
            IShellItem psiDestinationFolder,
            FileAttributes dwFileAttributes,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName,
            IFileOperationProgressSink pfopsItem
        );
        void PerformOperations();

        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetAnyOperationsAborted();
    }

    [DoesNotReturn]
    private static void ThrowLastWin32Error(string message)
    {
        throw new IOException(message, Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    }
}
