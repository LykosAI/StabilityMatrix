using System.Runtime.CompilerServices;

namespace StabilityMatrix.Native.Windows.Interop;

[GeneratedComInterface]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public partial interface IShellItemArray
{
    // uint BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out object ppvOut);

    /*[return: MarshalAs(UnmanagedType.Interface)]
    IShellItem GetItemAt(uint dwIndex);

    [return: MarshalAs(UnmanagedType.U4)]
    uint GetCount();*/

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void BindToHandler(
        [MarshalAs(UnmanagedType.Interface)] IntPtr pbc,
        ref Guid rbhid,
        ref Guid riid,
        out IntPtr ppvOut
    );

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    int GetCount();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    IShellItem GetItemAt(int dwIndex);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void EnumItems([MarshalAs(UnmanagedType.Interface)] out IntPtr ppenumShellItems);
}
