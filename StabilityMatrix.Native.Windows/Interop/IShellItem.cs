namespace StabilityMatrix.Native.Windows.Interop;

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
