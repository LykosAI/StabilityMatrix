namespace StabilityMatrix.Native.Windows.Interop;

[GeneratedComInterface]
[Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public partial interface IFileOperationProgressSink
{
    void StartOperations();
    void FinishOperations(uint hrResult);

    void PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
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
