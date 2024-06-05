namespace StabilityMatrix.Native.Windows.Interop;

[GeneratedComClass]
[Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2")]
public partial class FileOperationProgressSinkTcs : TaskCompletionSource<uint>, IFileOperationProgressSink
{
    private readonly IProgress<(uint WorkTotal, uint WorkSoFar)>? progress;

    public FileOperationProgressSinkTcs() { }

    public FileOperationProgressSinkTcs(IProgress<(uint, uint)> progress)
    {
        this.progress = progress;
    }

    /// <inheritdoc />
    public virtual void StartOperations() { }

    /// <inheritdoc />
    public virtual void FinishOperations(uint hrResult)
    {
        SetResult(hrResult);
    }

    /// <inheritdoc />
    public virtual void PreRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName) { }

    /// <inheritdoc />
    public virtual void PostRenameItem(
        uint dwFlags,
        IShellItem psiItem,
        string pszNewName,
        uint hrRename,
        IShellItem psiNewlyCreated
    ) { }

    /// <inheritdoc />
    public virtual void PreMoveItem(
        uint dwFlags,
        IShellItem psiItem,
        IShellItem psiDestinationFolder,
        string pszNewName
    ) { }

    /// <inheritdoc />
    public virtual void PostMoveItem(
        uint dwFlags,
        IShellItem psiItem,
        IShellItem psiDestinationFolder,
        string pszNewName,
        uint hrMove,
        IShellItem psiNewlyCreated
    ) { }

    /// <inheritdoc />
    public virtual void PreCopyItem(
        uint dwFlags,
        IShellItem psiItem,
        IShellItem psiDestinationFolder,
        string pszNewName
    ) { }

    /// <inheritdoc />
    public virtual void PostCopyItem(
        uint dwFlags,
        IShellItem psiItem,
        IShellItem psiDestinationFolder,
        string pszNewName,
        uint hrCopy,
        IShellItem psiNewlyCreated
    ) { }

    /// <inheritdoc />
    public virtual void PreDeleteItem(uint dwFlags, IShellItem psiItem) { }

    /// <inheritdoc />
    public virtual void PostDeleteItem(
        uint dwFlags,
        IShellItem psiItem,
        uint hrDelete,
        IShellItem psiNewlyCreated
    ) { }

    /// <inheritdoc />
    public virtual void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName) { }

    /// <inheritdoc />
    public virtual void PostNewItem(
        uint dwFlags,
        IShellItem psiDestinationFolder,
        string pszNewName,
        string pszTemplateName,
        uint dwFileAttributes,
        uint hrNew,
        IShellItem psiNewItem
    ) { }

    /// <inheritdoc />
    public virtual void UpdateProgress(uint iWorkTotal, uint iWorkSoFar)
    {
        progress?.Report((iWorkTotal, iWorkSoFar));
    }

    /// <inheritdoc />
    public virtual void ResetTimer() { }

    /// <inheritdoc />
    public virtual void PauseTimer() { }

    /// <inheritdoc />
    public virtual void ResumeTimer() { }
}
