using System.Runtime.InteropServices;

namespace StabilityMatrix.Core.Helper.HardwareInfo;

[StructLayout(LayoutKind.Sequential)]
public struct Win32MemoryStatusEx
{
    public uint DwLength = (uint)Marshal.SizeOf(typeof(Win32MemoryStatusEx));
    public uint DwMemoryLoad = 0;
    public ulong UllTotalPhys = 0;
    public ulong UllAvailPhys = 0;
    public ulong UllTotalPageFile = 0;
    public ulong UllAvailPageFile = 0;
    public ulong UllTotalVirtual = 0;
    public ulong UllAvailVirtual = 0;
    public ulong UllAvailExtendedVirtual = 0;

    public Win32MemoryStatusEx() { }
}
