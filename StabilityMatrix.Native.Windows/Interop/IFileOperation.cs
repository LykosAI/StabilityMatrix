namespace StabilityMatrix.Native.Windows.Interop;

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
        IFileOperationProgressSink? pfopsItem
    );
    void RenameItems(
        [MarshalAs(UnmanagedType.Interface)] object pUnkItems,
        [MarshalAs(UnmanagedType.LPWStr)] string pszNewName
    );
    void MoveItem(
        IShellItem psiItem,
        IShellItem psiDestinationFolder,
        [MarshalAs(UnmanagedType.LPWStr)] string pszNewName,
        IFileOperationProgressSink? pfopsItem
    );
    void MoveItems([MarshalAs(UnmanagedType.Interface)] object punkItems, IShellItem psiDestinationFolder);
    void CopyItem(
        IShellItem psiItem,
        IShellItem psiDestinationFolder,
        [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName,
        IFileOperationProgressSink? pfopsItem
    );
    void CopyItems([MarshalAs(UnmanagedType.Interface)] object punkItems, IShellItem psiDestinationFolder);
    void DeleteItem(IShellItem psiItem, IFileOperationProgressSink? pfopsItem);
    void DeleteItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
    uint NewItem(
        IShellItem psiDestinationFolder,
        FileAttributes dwFileAttributes,
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName,
        IFileOperationProgressSink? pfopsItem
    );
    void PerformOperations();

    [return: MarshalAs(UnmanagedType.Bool)]
    bool GetAnyOperationsAborted();
}
